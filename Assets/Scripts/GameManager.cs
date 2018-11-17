using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.MagicLeap;
using PolyToolkit;
using UnityEngine.UI;
using System.Linq;

public class GameManager : MonoBehaviour {
	[SerializeField, Tooltip("The reference to the class to handle results from.")]
	private BaseRaycast _raycast;

	[SerializeField, Tooltip("Debug Canvas")]
	private GameObject debugCanvas;

	[SerializeField, Tooltip("Debug Canvas")]
	private Text statusText;

	[SerializeField, Tooltip("Poly Object to Search For")]
	public Text polySearchText;

	[SerializeField, Tooltip("Head Camera")]
	public Camera _mainCamera;

	[SerializeField, Tooltip("Remote raycast visualizer")]
	public GameObject raycastCursor;

	[SerializeField, Tooltip("The default distance for the cursor when a hit is not detected.")]
	private float _defaultDistance = 7.0f;

	private GameObject _objectBeingPlaced;

	public string lastSearchKeywords; //TODO figure out whether I can send multiple keywords

	// Use this for initialization
	void Start () {
		// // Debug.Log("Requesting asset..."); //uncomment when building for ML
		// PolyApi.GetAsset("assets/5vbJ5vildOq", GetAssetCallback);

		// statusText.text = "Requesting...";
		
		// MLInput.OnControllerButtonDown += OnButtonDown;
		// MLInput.OnTriggerDown += OnTriggerDown;
  	}

	public PolyAsset getBestPolyAsset(string keyword, List<PolyAsset> assets)
	{
		List<PolyAsset> madeByGoogle = new List<PolyAsset>();
		Dictionary<PolyAsset, int> levDists = new Dictionary<PolyAsset, int>();

		foreach(var asset in assets)
		{
			if(asset.authorName.Contains("Google"))
			{
				madeByGoogle.Add(asset); 
			}

			levDists.Add(asset, LevenshteinDistance.Compute(keyword.ToUpper(), asset.displayName.ToUpper())); 
		}

		PolyAsset chosenAsset = null;

		var sortedLevDists = (from kv in levDists orderby kv.Value select kv).ToList();

		foreach(var kvp in sortedLevDists)
		{
			var levDist = kvp.Value;
			var asset = kvp.Key;

			if(madeByGoogle.Contains(asset) && levDist- sortedLevDists[0].Value < 3) //could look for more words here
			{
				chosenAsset = asset;
				break;
			}
		}

		if (chosenAsset == null)
		{
			chosenAsset = sortedLevDists[0].Key;
		}

		return chosenAsset;
	}

	private void ListAssetsCallback(PolyStatusOr<PolyListAssetsResult> result)
	{
		if(!result.Ok) return;

		List<PolyAsset> assets = result.Value.assets;

		statusText.text = "Importing...";

		PolyApi.Import(getBestPolyAsset(lastSearchKeywords, assets), makeDefaultImportOptions(), ImportAssetCallback);
	}

	public PolyImportOptions makeDefaultImportOptions()
	{
		// Set the import options.
		PolyImportOptions options = PolyImportOptions.Default();
		// We want to rescale the imported mesh to a specific size.
		options.rescalingMode = PolyImportOptions.RescalingMode.FIT;

		options.desiredSize = 0.15f;
		// We want the imported assets to be recentered such that their centroid coincides with the origin:
		options.recenter = true;

		return options;
	}

  	// Callback invoked when the featured assets results are returned.
	private void GetAssetCallback(PolyStatusOr<PolyAsset> result) {
		if (!result.Ok) {
			Debug.LogError("Failed to get assets. Reason: " + result.Status);
			statusText.text = "ERROR: " + result.Status;
			return;
		}

		Debug.Log("Successfully got asset!");

		statusText.text = "Importing...";
		PolyApi.Import(result.Value, makeDefaultImportOptions(), ImportAssetCallback);
	}

  	// Callback invoked when an asset has just been imported.
	private void ImportAssetCallback(PolyAsset asset, PolyStatusOr<PolyImportResult> result) {
		if (!result.Ok) {
			Debug.LogError("Failed to import asset. :( Reason: " + result.Status);
			statusText.text = "ERROR: Import failed: " + result.Status;
			return;
		}

		Debug.Log("Successfully imported asset!");

		// Show attribution (asset title and author).
		statusText.text = asset.displayName + "\nby " + asset.authorName;

		// var forwardVec = _mainCamera.transform.forward;
		// forwardVec.y = 0;
		// forwardVec = forwardVec.normalized;

		// result.Value.gameObject.transform.position = _mainCamera.transform.position + forwardVec * 0.8f;

		_objectBeingPlaced = result.Value.gameObject;
		raycastCursor.SetActive(false);

		result.Value.gameObject.AddComponent<Rotate>();
	}

	#region Event Handlers
	private void OnButtonDown(byte controllerId, MLInputControllerButton button)
	{
		if(button == MLInputControllerButton.Bumper)
		{
			debugCanvas.SetActive(!debugCanvas.activeSelf);
		}
	}
	
	private void OnTriggerDown(byte controllerId, float triggerVal)
	{
		if(_objectBeingPlaced != null)
		{
			_objectBeingPlaced = null;
			raycastCursor.SetActive(true);
		}
		else if(!string.IsNullOrEmpty(polySearchText.text))
		{
			SearchPoly(polySearchText.text, ListAssetsCallback);
			polySearchText.text = "";
		}
	}

	public void SearchPoly(string text, PolyApi.ListAssetsCallback callback)
	{
		var request = new PolyListAssetsRequest();
		request.curated = true;
		request.keywords = text;

		lastSearchKeywords = request.keywords;

		PolyApi.ListAssets(request, callback);  
	}

	/// <summary>
	/// Callback handler called when raycast has a result.
	/// Updates the transform an color on the Hit Position and Normal from the assigned object.
	/// </summary>
	/// <param name="state"> The state of the raycast result.</param>
	/// <param name="result"> The hit results (point, normal, distance).</param>
	/// <param name="confidence"> Confidence value of hit. 0 no hit, 1 sure hit.</param>
	public void OnRaycastHit(MLWorldRays.MLWorldRaycastResultState state, RaycastHit result, float confidence)
	{
		if(_objectBeingPlaced != null)
		{
			if (state == MLWorldRays.MLWorldRaycastResultState.RequestFailed || state == MLWorldRays.MLWorldRaycastResultState.NoCollision)
			{
				// No hit found, set it to default distance away from controller ray
				_objectBeingPlaced.transform.position = (_raycast.RayOrigin + (_raycast.RayDirection * _defaultDistance));
				//_objectBeingPlaced.transform.LookAt(_raycast.RayOrigin);
			}
			else
			{
				// Hit found -- Update the object's position and normal.
				_objectBeingPlaced.transform.position = result.point;
				_objectBeingPlaced.transform.up = result.normal;
				// var originPoint = _raycast.RayOrigin;
				// originPoint.y = result.point.y; //put camera + hit on the same plane...

				//_objectBeingPlaced.transform.LookAt(originPoint); //...so lookat only affects y axis rotation
			}	
		}
    }
	#endregion

	// Update is called once per frame
	void Update () {
		
	}
}
