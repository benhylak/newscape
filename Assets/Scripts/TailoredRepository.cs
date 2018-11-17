using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ScoredSearchResult = NewsScript.ScoredSearchResult;
using ConcepnetDotNet;
using Humanizer;

public class TailoredRepository : MonoBehaviour { 

	private ConceptNetWrapper _conceptNet;
	private TailoredResponse[] _availableResponses;

	// Use this for initialization
	void Start () {
		this._availableResponses = GetComponents<TailoredResponse>();
		_conceptNet = new ConceptNetWrapper("http://api.conceptnet.io/");
	}

	public ScoredSearchResult Search(string searchTerm)
	{
		double bestScore = double.MinValue;
		string bestAssetTerm = "";
		TailoredResponse bestResponse = null;

		foreach(var potentialResponse in _availableResponses)
		{
			foreach(string assetTerm in potentialResponse.matchingTerms)
			{
				double score = _conceptNet.GetRelationScore(searchTerm, assetTerm);

				if(score > bestScore)
				{
					bestAssetTerm = assetTerm;
					bestResponse = potentialResponse;
					bestScore = score;
				}
			}
		}

		if(bestResponse == null)
		{
			return new ScoredSearchResult();
		}
		else if(bestScore == 1)
		{
			bestScore = 1.5;
		}
		else if(bestScore > .65f)
		{
			bestScore = 1;
		}

		if(bestScore < 0.5) bestScore = 0.1;

		return new ScoredSearchResult(bestResponse.equivalentModels[0], bestScore, bestAssetTerm);
	}
}
