using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NewsAPI;
using NewsAPI.Constants;
using NewsAPI.Models;
using System;
using UnityEngine.UI;
using PolyToolkit;
using System.Text.RegularExpressions;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using System.IO;
using FrostweepGames.Plugins.GoogleCloud.NaturalLanguage;
using ConcepnetDotNet;
using System.Threading;
using System.Threading.Tasks;

public class NewsScript : MonoBehaviour {

	public struct ScoredSearchResult
	{
		public enum SearchType { POLY, TAILORED };

		public ScoredSearchResult(PolyAsset polyAsset, double score)
		{
			searchType = SearchType.POLY;

			this.polyAsset = polyAsset;
			this.score = score; 
			this.tailoredAsset = null;
			this.name = polyAsset.displayName;
		}

		public ScoredSearchResult(GameObject tailoredAsset, double score, string name)
		{
			searchType = SearchType.TAILORED;

			this.tailoredAsset = tailoredAsset;
			this.score = score;
			this.name = name;
			this.polyAsset = null;
		}

		public SearchType searchType;
		public GameObject tailoredAsset;
		public PolyAsset polyAsset;
		public double score;
		public string name;
	}

	private TailoredRepository TailoredRepository;
	public InputField searchText;
	public Dictionary<string, int> dF;
	private NewsApiClient newsApiClient;

	private static string WORD_FREQUENCY_CSV_PATH = "Assets/Resources/headline_word_counts.csv";

	private GCNaturalLanguage _gcNaturalLanguage;

	private static int NUMBER_OF_HEADLINES_SCANNED = 1000000;

	private ConceptNetWrapper _conceptNet;

	private Aliaser aliaser = Aliaser.makeDefaultAliaser();

	// Use this for initialization
	async void Start () {
		TailoredRepository = GetComponentInChildren<TailoredRepository>();

		dF = new Dictionary<string, int>();

		_conceptNet = new ConceptNetWrapper("http://api.conceptnet.io/");

		//TODO: should have a variety of catgs:  business entertainment general health science sports technology
		
        // init with your API key (get this from newsapi.org)
		newsApiClient = new NewsApiClient("4235111de41841c2a22049677d24ccb7");

		using (var reader = new StreamReader(WORD_FREQUENCY_CSV_PATH))
		using (var csvReader = new CsvReader(reader))
		{
			csvReader.Read();
			csvReader.ReadHeader();

			while (csvReader.Read())
			{
				string word = csvReader.GetField("word");
				int count = int.Parse(csvReader.GetField("count"));

				dF[word] = count;
			}
		}

		Debug.Log(String.Format("Words loaded: {0}", dF.Count()));
		Debug.Log(String.Format("Trump mentioned {0} times.", dF["trump"]));

		// var client = LanguageServiceClient.Create(); 

		// var response = client.AnalyzeSentiment(new Document() 
		// {
		// 	Content = text,
		// 	Type = Document.Types.Type.PlainText
		// });

        // var sentiment = response.DocumentSentiment;

		var articlesResult = await newsApiClient.GetTopHeadlinesAsync(new TopHeadlinesRequest
		{
			Language = Languages.EN,
			Country = Countries.US, //TODO: blend countries, but weight current country
			PageSize = 50
		});

		if (articlesResult.Status == Statuses.Ok)
		{
			// total results found
			Debug.Log(articlesResult.TotalResults);

			foreach (var article in articlesResult.Articles)
			{
				// title
				Debug.Log(article.Title);

				// description
				Debug.Log(article.Description + "\n " + article.Source.Id);
				// // url
				// Debug.Log(article.Url);
				// // image
				// Debug.Log(article.UrlToImage);
				// // published at
				Debug.Log(article.PublishedAt);
			}
		}
		else Debug.Log(articlesResult.Error.Message);

		_gcNaturalLanguage = GCNaturalLanguage.Instance;

		_gcNaturalLanguage.AnnotateTextSuccessEvent += _gcNaturalLanguage_AnnotateTextSuccessEvent;
		_gcNaturalLanguage.AnnotateTextFailedEvent += _gcNaturalLanguage_Failure;
	}

	public void OnSearch()
	{
		AnalyzeEntities(searchText.text, Enumerators.Language.en);
	}

	public void AddFromPoly(PolyAsset asset)
	{
		PolyApi.Import(
			asset, 
			PolyImportOptions.Default(),
			(_, res) => 
			{
				Debug.Log("Best asset: " + _.displayName + " by: " + _.authorName);

				if (!res.Ok) {
					Debug.LogError("Failed to import asset. :( Reason: " + res.Status);
					return;
				}

				res.Value.gameObject.AddComponent<Rotate>();
			});
	}

	private void ListAssetsCallback(PolyStatusOr<PolyListAssetsResult> result)
	{
		if(!result.Ok) return;
	}

	public async Task<ScoredSearchResult> SearchPoly(string term)
	{
		Debug.Log("Searching Poly for term: " + term);

		var request = new PolyListAssetsRequest();
		request.orderBy = PolyOrderBy.BEST;
		request.keywords = term;
		request.curated = true;

		PolyListAssetsResult result = null;

		bool error = false;

		PolyApi.ListAssets(request, response =>
		{
			if(response.Ok)
			{
				result = response.Value;
			}
			else
			{
				error = true;
			}			
		});

		while(result == null && !error) //TODO: Add Timeout to avoid infinite loop!
		{
			await Task.Delay(50);
		}

		await new WaitForBackgroundThread();

		PolyAsset bestAsset = null;
		double bestScore = double.MinValue;

		if(result != null)
		{
			foreach(var asset in result.assets)
			{
				//get how related it is to the search term
				//if doesn't exist in concept net, only accept exact matches (or lev dists <2)
				//made by poly is * 1.5 multiplier (can tweak)

				double score = -1;
				
				if(asset.displayName.ToLower().Equals(term))
				{
					score = 1;
				}
				else
				{
					score = _conceptNet.GetRelationScore(asset.displayName.ToLower(), term);
				}
				
				Debug.Log(asset.displayName + " by: " + asset.authorName + " (" + score + ") ");;

				if(asset.authorName.Contains("Google"))
				{
					score = score * 1.75;
				}

				if(score > bestScore)
				{
					bestScore = score;
					bestAsset = asset;
				}
			}
		}

		if(bestAsset!=null)
		{
			Debug.Log(
				String.Format("Term: {0}, Asset: {1}, Score: {2}", term, bestAsset.displayName + " by: " + bestAsset.authorName, bestScore));
		}
		else Debug.Log("No results for term: " + term);

		var bestResult = new ScoredSearchResult();

		bestResult.score = bestScore;
		bestResult.polyAsset = bestAsset;

		await new WaitForUpdate();

		return bestResult;
	}

	 public void AnalyzeEntities(string text, Enumerators.Language lang)
	{
		_gcNaturalLanguage.Annotate(new AnnotateTextRequest()
		{
			encodingType = Enumerators.EncodingType.UTF8,
			features = new Features
			{
				extractEntities = true,
				extractEntitySentiment = true,
				extractSyntax = true
			},
			document = new LocalDocument()
			{
				content = text,
				language = _gcNaturalLanguage.PrepareLanguage(lang),
				type = Enumerators.DocumentType.PLAIN_TEXT
			}
		});
	}

	private async void FindBestAssetForTerms(Dictionary<string, double> termScores, Dictionary<string, bool> termIsPersonMap)
	{
		double bestScore = double.MinValue;
		ScoredSearchResult bestAssetForTerm = new ScoredSearchResult();
		string bestTerm = "";

		Dictionary<string, Task<ScoredSearchResult>> polyLookupTasks = termScores.Keys.ToDictionary(x => x, x => SearchPoly(x));
		
		await new WaitForBackgroundThread();

		await Task.WhenAll(polyLookupTasks.Values.AsEnumerable());

		for(int i=0; i< termScores.Keys.Count; i++)
		{
			string term = termScores.Keys.ElementAt(i);

			var polyAssetResult = await polyLookupTasks[term];
			var tailoredAssetResult = TailoredRepository.Search(term);

			bool termIsPerson = false;
			termIsPersonMap.TryGetValue(term, out termIsPerson);

			ScoredSearchResult termSearchResult = termIsPerson || tailoredAssetResult.score > polyAssetResult.score ? tailoredAssetResult : polyAssetResult;

			//search tailored source, choose 
			Debug.Log(String.Format("Word: {0} Weight: {1}", term, termScores[term]));

			//TODO: handle the fact that the best could be either poly or tailored
			termScores[term] *= Math.Pow(termSearchResult.score, 2);

			if(termScores[term] > bestScore)
			{
				bestTerm = term;
				bestAssetForTerm = termSearchResult;
				bestScore = termScores[term];
			}
		}

		//priority queue of words and weights
		//three options for finding models
		// 1. Search through keywords until a model accuracy threhsold (e.g. .7) is surpassed
		// 2. Find highest combination of term relevancy and model accuracy
		// 3. Combine both? (Find highest in first 4 words, then if they don't pass threshold, continue)

		// 2 probably makes the most sense, given that this process doesn't need much optimizaition

		await new WaitForUpdate();

		if(bestAssetForTerm.searchType == ScoredSearchResult.SearchType.POLY)
		{
			//todo convert to await
			AddFromPoly(bestAssetForTerm.polyAsset);
		}
		else
		{
			var gameObject = GameObject.Instantiate(bestAssetForTerm.tailoredAsset);
			gameObject.name = bestAssetForTerm.name;
			gameObject.transform.position = Vector3.zero;
		}

		//GetComponent<GameManager>().lastSearchKeywords = bestAssetForTerm.name;

		Debug.Log("Best word: " + bestTerm);
	}

	private Dictionary<string, double> scoreAllTerms(AnnotateTextResponse annotationResult)
	{
		Dictionary<string, double> termScores = new Dictionary<string, double>();

		foreach (var item in annotationResult.entities)
		{
			string entityName = item.name.ToLower();
			if(aliaser.ContainsKey(entityName)) entityName = aliaser[entityName];

			string[] wordsInEntity = GetWords(entityName);
			
			if((item.type == Enumerators.EntityType.PERSON || wordsInEntity.Count() > 1)) //grizzly bear, power lines, supreme court
			{
				var idf = getIdf(wordsInEntity);
				termScores[entityName] = getTermRelevancyScore(idf, item.salience);

				Debug.Log(String.Format("Word: {0} Term Salience: {1} Word IDF: {2} Score: {3}", item.name, item.salience, idf, termScores[entityName]));
			}

			if(item.type != Enumerators.EntityType.PERSON) //if not a person, try the terms individually as well
			{
				foreach(var word in wordsInEntity)
				{
					var idf = getIdf(word);
					termScores[word] = getTermRelevancyScore(idf, item.salience);

					Debug.Log(String.Format("Word: {0} Term Salience: {1} Word IDF: {2} Score: {3}", word, item.salience, idf, termScores[word]));
				}
			}
		}

		return termScores;
	}
	
	private void _gcNaturalLanguage_AnnotateTextSuccessEvent(AnnotateTextResponse annotationResult)
	{
		Debug.Log("Annotate Success!");

		//Func<Entity, bool> personProperFilter = (x) => x.type != Enumerators.EntityType.PERSON && x.mentions.First().type != Enumerators.EntityMentionType.PROPER;

		Dictionary<string, double> termScores = scoreAllTerms(annotationResult);
		Dictionary<string, bool> termIsPersonMap = annotationResult.entities.ToDictionary(x => x.name, y => y.type == Enumerators.EntityType.PERSON);

		FindBestAssetForTerms(termScores, termIsPersonMap);
	}

	private double getTermRelevancyScore(double idf, double salience)
	{
		return idf * idf * Math.Sqrt(salience);
	}

	private double getIdf(string word)
	{
		int count = dF.ContainsKey(word.ToLower()) ? dF[word.ToLower()] : 0;
		count += 1; //a document that wasn't counted has this word. prevents count from ever being 0 + messing up log

		return Math.Log10(NUMBER_OF_HEADLINES_SCANNED / count) / Math.Log10(NUMBER_OF_HEADLINES_SCANNED); //normalized idf (0->1)
	}

	private double getIdf(string[] wordsInTerm)
	{
		var idfs = from word in wordsInTerm select getIdf(word);
		return idfs.Max();
	}

	#region failed handlers
	private void _gcNaturalLanguage_Failure(string obj)
	{
		Debug.Log("Error");
		Debug.Log(obj);
	}
	#endregion failed handlers

	static string[] GetWords(string input)
	{
		MatchCollection matches = Regex.Matches(input, @"\b[\w']*\b");

		var words = from m in matches.Cast<Match>()
					where !string.IsNullOrEmpty(m.Value)
					select m.Value;

		return words.ToArray();
	}
}

//matching entity to parts of speech... (verb, noun, etc.)

// var textToPartOfSpeech = new Dictionary<double, PartOfSpeech>();
// // foreach (var token in obj.tokens)
// // {
// // 	textToPartOfSpeech[token.text.beginOffset] = token.partOfSpeech;
// // }
// var textSpan = item.mentions.First(i => textToPartOfSpeech.ContainsKey(i.text.beginOffset)).text.beginOffset;
// var partOfSpeech = textToPartOfSpeech[textSpan];