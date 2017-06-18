/*Copyright 2015 Sean Finch

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.*/
using UnityEngine;
using Steamworks;
using System.Threading;
using System.Collections.Generic;
public class PersistData:MonoBehaviour {
	public enum GT { QuickPlay = 0, Arcade = 1, Campaign = 2, Versus = 3, Training = 4, Challenge = 5, PlayerData = 6, Options = 7 }
	public enum GS { Intro = 0, MainMenu = 1, CharSel = 2, Game = 3, PuzSel = 4, CutScene = 5, HighScore = 6, PlayerData = 7, Credits = 8, WinnerIsYou = 9, Options = 10,
					 RoundWinner = 11, OpeningScene = 12 }
	public enum C { Null = -1, George = 0, Milo, Devin, MJ, Andrew, Joan, Depeche, Lars, Laila, AliceAna, White, September, Everyone, FuckingBalloon }
	public C p1Char, p2Char;
	private GS currentScreen;
	public GT gameType;
	public int unlockNew, demoPlayers, level, puzzleType, initialDifficulty, difficulty, rounds, currentRound, rowCount, rowCount2, totalRoundTime, totalP1RoundScore, totalP2RoundScore, winType, runningScore, runningTime, prevMainMenuLocationX, prevMainMenuLocationY, balloonType;
	public bool won, useSpecial, isTutorial, isDemo, override2P, isTransitioning, aboutToFightAFuckingBalloon, usingMouse;
	public bool usingGamepad1, usingGamepad2;
	public List<bool> playerOneWonRound;
	public List<int> playerRoundScores, playerRoundTimes;
	public InputMethod controller, controller2;
	private SaveData saveInfo;
	public FontData mostCommonFont;
	public AudioContainerContainer sounds;
	public GameObject universalPrefab, universalPrefabCollider;
	public string culture = "en";
	public int KEY_DELAY;
	private static SteamLeaderboard_t storyscore, storytime, endlessscore, endlesstime;
	private static CallResult<LeaderboardFindResult_t> m_findResult;
	private static CallResult<LeaderboardScoreUploaded_t> m_uploadResult;
	private static Callback<UserStatsReceived_t> Callback_statsReceived;
	void Start() {
		Object.DontDestroyOnLoad(this);
		universalPrefab = Resources.Load<GameObject>("Prefabs/Tile_NoCollider");
		universalPrefabCollider = Resources.Load<GameObject>("Prefabs/Tile");
		Object.DontDestroyOnLoad(universalPrefab);
		Texture2D t = Resources.Load<Texture2D>(SpritePaths.MouseCursor);
		Cursor.SetCursor(t, Vector2.zero, CursorMode.ForceSoftware);
		prevMainMenuLocationX = -1;
		prevMainMenuLocationY = 4;
		p1Char = C.Null;
		p2Char = C.Null;
		usingGamepad1 = false;
		usingGamepad2 = false;
		initialDifficulty = 4;
		difficulty = 4;
		unlockNew = 0;
		isDemo = false;
		dontFade = false;
		isTransitioning = false;
		mostCommonFont = new FontData(TextAnchor.UpperCenter, TextAlignment.Center, 0.03f);
		SetupFadeVars();
		LoadGeemu();
		StartCoroutine(SameScreenSave(false, true));
		KEY_DELAY = saveInfo.savedOptions["keydelay"];
		SetRes();
		override2P = false;
		forceOnlinePause = false;
		if(SteamManager.Initialized) {
			m_findResult = new CallResult<LeaderboardFindResult_t>();
			m_uploadResult = new CallResult<LeaderboardScoreUploaded_t>();
			Callback_statsReceived = Callback<UserStatsReceived_t>.Create(OnGetUserStats);
			intQueue = new List<KeyValuePair<string, int>>();
			SteamAPICall_t h1 = SteamUserStats.FindLeaderboard("STORYMODESCORE");
			m_findResult.Set(h1, StoryScoreUpload);
			new Timer(timer_tick, null, 0, 1000);
		}
	}
	#region "Steam"
	private static void timer_tick(object state) { SteamAPI.RunCallbacks(); }
	private static void StoryScoreUpload(LeaderboardFindResult_t pCallback, bool failure) { 
		storyscore = pCallback.m_hSteamLeaderboard;
		SteamAPICall_t h2 = SteamUserStats.FindLeaderboard("STORYMODETIME");
		m_findResult.Set(h2, StoryTimeUpload);
	}
	private static void StoryTimeUpload(LeaderboardFindResult_t pCallback, bool failure) {
		storytime = pCallback.m_hSteamLeaderboard;
		SteamAPICall_t h3 = SteamUserStats.FindLeaderboard("ENDLESSMODESCORE");
		m_findResult.Set(h3, EndlessScoreUpload);
	}
	private static void EndlessScoreUpload(LeaderboardFindResult_t pCallback, bool failure) { 
		endlessscore = pCallback.m_hSteamLeaderboard;
		SteamAPICall_t h4 = SteamUserStats.FindLeaderboard("ENDLESSMODETIME");
		m_findResult.Set(h4, EndlessTimeUpload);
	}
	private static void EndlessTimeUpload(LeaderboardFindResult_t pCallback, bool failure) { endlesstime = pCallback.m_hSteamLeaderboard; }

	public void SetArcadeWinAchivements(bool justWon, int startDiff) {
		if(saveInfo.getArcadeVictories() >= 1) { SetAchievement("STANDARD_STORY"); }
		if(saveInfo.getArcadeVictoryCharacterWhiteWins() == 10) { SetAchievement("STANDARD_ALL"); }
		if(saveInfo.hasAnyDragonWins()) { SetAchievement("STORY_DRAGON"); }
		if(saveInfo.getArcadeVictoryCharacterSeptemberWins() == 10) { SetAchievement("DRAGON_ALL"); }
		if(justWon && startDiff == 9) { SetAchievement("REEL_TUFF_GUY"); }
		else if(justWon && startDiff > 5) { SetAchievement("TUFF_GUY"); }
	}
	public bool SetAchievement(string id) {
		if(!SteamManager.Initialized) { return false; }
		bool result = SteamUserStats.SetAchievement(id);
		if(result) {
			SteamUserStats.StoreStats();
			Debug.Log ("Unlocked achievement " + id);
		} else {
			Debug.Log ("Failed to unlock achievement " + id);
		}
		return result;
	}

	private void OnGetUserStats(UserStatsReceived_t pCallback) {
		foreach(KeyValuePair<string, int> b in intQueue) {
			IncrementIntStat(b.Key, b.Value);
		}
		intQueue.Clear();
	}

	private bool alreadyCalled = false;
	private List<KeyValuePair<string, int>> intQueue;

	private void UpdateWinStats() {
		switch(p1Char) {
			case C.AliceAna: IncrementIntStat("ALICE", 1); break;
			case C.Andrew: IncrementIntStat("ANDREW", 1); break;
			case C.Depeche: IncrementIntStat("MODE", 1); break;
			case C.Devin: IncrementIntStat("DEVIN", 1); break;
			case C.George: IncrementIntStat("GEORGE", 1); break;
			case C.Joan: IncrementIntStat("JOAN", 1); break;
			case C.Laila: IncrementIntStat("LAILA", 1); break;
			case C.Lars: IncrementIntStat("LARS", 1); break;
			case C.Milo: IncrementIntStat("MILO", 1); break;
			case C.MJ: IncrementIntStat("MJ", 1); break;
			default: IncrementIntStat("OTHER", 1); break;
		}
		if(gameType != GT.Versus) { IncrementIntStat("MATCH_WINS", 1); }
		IncrementIntStat("MATCH_TIMES", runningTime);
	}
	private bool IncrementIntStat(string id, int amount) {
		if(!SteamManager.Initialized) { return false; }
		int stat = 0;
		if(!SteamUserStats.GetStat(id, out stat)) {
			intQueue.Add(new KeyValuePair<string, int>(id, amount));
			if(!alreadyCalled) {
				Debug.Log ("Retrying " + id);
				SteamUserStats.RequestCurrentStats();
				alreadyCalled = true;
			}
			return false;
			//Debug.Log ("Couldn't even get the data for " + id);
		}
		stat += amount;
		bool result = SteamUserStats.SetStat(id, stat);
		if(result) {
			Debug.Log ("Set stat " + id + " to " + stat);
			SteamUserStats.StoreStats();
		} else {
			Debug.Log ("Failed to set stat " + id);
		}
		return result;
	}
	public void SetEndlessScores() {
		SetLeaderboard(endlessscore, runningScore);
		SetLeaderboard(endlesstime, runningTime);
	}
	public void SetStoryScores() {
		SetLeaderboard(storyscore, runningScore);
		SetLeaderboard(storytime, runningTime);
	}
	private void SetLeaderboard(SteamLeaderboard_t board, int score) {
		if(!SteamManager.Initialized) { return; }
		SteamAPICall_t hSteamAPICall = SteamUserStats.UploadLeaderboardScore(board, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest, score, null, 0);
	}
	#endregion
	#region "Tile Bank"
	private List<GameObject> GameObjectBank;
	public GameObject GetBankObject() {
		if(GameObjectBank.Count == 0) { return null; }
		GameObject g = GameObjectBank[0];
		GameObjectBank.Remove(g);
		if(g == null) { return null; }
		g.SetActive(true);
		g.transform.rotation = Quaternion.identity;
		return g;
	}
	public void AddToBank(GameObject g) {
		GameObjectBank.Add(g);
		g.transform.parent = null;
		g.SetActive(false);
	}
	public void InitGameObjectBank() {
		if(GameObjectBank == null) { GameObjectBank = new List<GameObject>(); }
		int count = gameType == GT.Versus ? 900 : 450;
		for(int i = 0; i < count; i++) {
			GameObjectBank.Add(Instantiate(universalPrefab, Vector3.zero, Quaternion.identity) as GameObject);
		}
	}
	public void ClearGameObjectBank() {
		for(int i = 0; i < GameObjectBank.Count; i++) {
			Destroy(GameObjectBank[i]);
		}
		GameObjectBank.Clear();
	}
	#endregion
	#region "Controller Setup"
	private bool IsKeyboardRegisteringAsGamepad() { return Input.GetJoystickNames()[0].IndexOf("abcdefg") >= 0; }
	public int GetGamepadsPresent() {
		string[] gamepads = Input.GetJoystickNames();
		if(gamepads.Length == 0) { return 0; }
		bool keyboardPresent = IsKeyboardRegisteringAsGamepad();
		if(gamepads.Length == 1 && keyboardPresent) { return 0; }
		return keyboardPresent ? (gamepads.Length - 1) : gamepads.Length;
	}
	public void UpdateGamepad(int player, int buttonIdx) {
		int analogIdx = buttonIdx + 1;
		string buttonPrefix = (35 + buttonIdx * 2).ToString();
		string analogPrefix = "joy" + analogIdx;
		saveInfo.UpdateGamepadNumber(player, buttonPrefix, analogPrefix);
		StartCoroutine(SameScreenSave());
	}

	public bool IsKeyDownOrButtonPressed() {
		for(int i = 0; i < 4; i++) {
			if(Input.GetKeyDown((KeyCode)(350 + 20 * i))) { return true; }
			if(Input.GetKeyDown((KeyCode)(357 + 20 * i))) { return true; }
		}
		return Input.inputString.Length > 0 || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow);
	}
	private InputVal GetInputVal(string binding) {
		if(binding.Contains(":")) {
			string[] split = binding.Split(new char[]{':'});
			return new InputVal_Axis(split[0], int.Parse(split[1]));
		} else {
			return new InputVal_Key(int.Parse(binding));
		}
	}
	public string GetP1InputName(InputMethod.KeyBinding binding) { return GetInputVal(saveInfo.controlBindingsP1[(int)binding]).GetName(); }
	public string GetP2InputName(InputMethod.KeyBinding binding) { return GetInputVal(saveInfo.controlBindingsP2[(int)binding]).GetName(); }
	public int ReturnLaunchOrPauseOrNothingIsPressed() {
		for(int i = 0; i < 4; i++) {
			if(Input.GetKeyDown((KeyCode)(350 + 20 * i))) { return 1; }
			if(Input.GetKeyDown((KeyCode)(357 + 20 * i))) { return 2; }
		}
		InputVal launch = GetInputVal(saveInfo.GetBinding(InputMethod.KeyBinding.launch, 0, usingGamepad1));
		InputVal pause = GetInputVal(saveInfo.GetBinding(InputMethod.KeyBinding.pause, 0, usingGamepad1));
		if(Input.GetMouseButtonDown(0) || launch.KeyDown()) { return 1; }
		if(pause.KeyDown()) { return 2; }
		return 0;
	}
	public InputMethod GetP1Controller() {
		if(ReturnLaunchOrPauseOrNothingIsPressed() > 0) { return new Input_Computer(); }
		return null;
	}

	public InputMethod detectInput_P2() {
		if(usingGamepad2) {
			for(int i = 0; i < 4; i++) {
				if(Input.GetKeyDown((KeyCode)(350 + 20 * i))) {
					UpdateGamepad(1, i);
					break;
				}
			}
		}
		InputVal launch = GetInputVal(saveInfo.GetBinding(InputMethod.KeyBinding.launch, 1, usingGamepad2));
		InputVal pause = GetInputVal(saveInfo.GetBinding(InputMethod.KeyBinding.pause, 1, usingGamepad2));
		if(launch.KeyDown() || pause.KeyDown()) { return new Input_Computer(); }
		return null;
	}
	#endregion
	#region "Transitions"
	private bool isFading, holdFade;
	private int fadeDir;
	private Texture2D fade;
	private float fadeSpeed, fadeAlpha;
	public bool dontFade;
	private void SetupFadeVars() {
		isFading = false;
		fadeSpeed = 1.5f;
		fadeAlpha = 0.0f;
		holdFade = false;
		fadeDir = -1;
		fade = Resources.Load<Texture2D>(SpritePaths.FullBlackCover);
	}
	public void OnGUI() {
		//ShowFPS();
		if(isFading) {
			fadeAlpha += fadeDir * fadeSpeed * Time.deltaTime;
			fadeAlpha = Mathf.Clamp01(fadeAlpha);
			if(fadeAlpha == 0.0f || fadeAlpha == 1.0f) { isFading = false; }
			Color g = GUI.color;
			g.a = fadeAlpha;
			GUI.color = g;
			GUI.depth = -1000;
			GUI.DrawTexture(new Rect(0.0f, 0.0f, Screen.width, Screen.height), fade);
		} else if(holdFade) {
			GUI.DrawTexture(new Rect(0.0f, 0.0f, Screen.width, Screen.height), fade);
		}
	}
	float deltaTime = 0.0f;
	void Update() { deltaTime += (Time.deltaTime - deltaTime) * 0.1f; }
	void ShowFPS() {
		int w = Screen.width, h = Screen.height;
		GUIStyle style = new GUIStyle();
		Rect rect = new Rect(0, 0, w, h * 2 / 100);
		style.alignment = TextAnchor.UpperLeft;
		style.fontSize = h * 2 / 60;
		style.normal.textColor = new Color (1.0f, 1.0f, 1.0f, 1.0f);
		float msec = deltaTime * 1000.0f;
		float fps = 1.0f / deltaTime;
		string text = string.Format("{1:0.} fps\r\n{2} GO\r\n{0:0.0} ms", msec, fps, GameObject.FindObjectsOfType(typeof(GameObject)).Length);
		GUI.Label(rect, text, style);
	}


	public void SaveAndQuit(int time) { saveInfo.addPlayTime(gameType, time); StartCoroutine(SameScreenSave(true)); }
	public void SaveAndReset(int time) { saveInfo.addPlayTime(gameType, time); StartCoroutine(ChangeScreenAndSave(GS.Game)); }
	public void SaveAndMainMenu(int time) { saveInfo.addPlayTime(gameType, time); StartCoroutine(ChangeScreenAndSave(GS.MainMenu)); }
	public void SaveAndPuzzleSelect(int time) { saveInfo.addPlayTime(gameType, time); StartCoroutine(ChangeScreenAndSave(GS.PuzSel)); }
	public void GoToMainMenu() { ChangeScreen(GS.MainMenu); }
	public void ChangeScreen(GS type) {
		if(isTransitioning) { return; }
		if(currentScreen == GS.Game) { ClearGameObjectBank(); }
		isTransitioning = true;
		StartFade(1);
		StartCoroutine(ChangeScreenInner(type));
	}
	public System.Collections.IEnumerator ChangeScreenAndSave(GS type) {
		GameObject g = Instantiate(universalPrefab, new Vector3(3.28f, 1.7f), Quaternion.identity) as GameObject;
		g.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>(SpritePaths.Saving);
		g.renderer.sortingLayerName = "Pause HUD Cursor";
		for(int i = 0; i < 10; i++) {
			if(g == null) { break; }
			g.transform.Rotate(0.0f, 0.0f, -3.0f);
			yield return new WaitForSeconds(0.01f);
		}
		SaveGeemu();
		for(int i = 0; i < 10; i++) {
			if(g == null) { break; }
			g.transform.Rotate(0.0f, 0.0f, -3.0f);
			yield return new WaitForSeconds(0.01f);
		}
		if(g != null) { Destroy(g); }
		ChangeScreen(type);
	}
	public System.Collections.IEnumerator SameScreenSave(bool quitAtEnd = false, bool isIntroScreen = false) {
		GameObject g = Instantiate(universalPrefab, new Vector3(3.28f, 1.7f), Quaternion.identity) as GameObject;
		GameObject meshText = null;
		g.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>(SpritePaths.Saving);
		g.renderer.sortingLayerName = "Pause HUD Cursor";
		int imax = 10;
		if(isIntroScreen) {
			meshText = Instantiate(Resources.Load<GameObject>("Prefabs/Text/Size48"), new Vector3(7.2f, 4.4f), Quaternion.identity) as GameObject;
			TextMesh mesh = meshText.GetComponent<TextMesh>();
			mesh.text = "Now Saving. Please do not power off your machine.";
			mesh.color = Color.white;
			mesh.alignment = TextAlignment.Right;
			mesh.anchor = TextAnchor.MiddleRight;
			meshText.renderer.sortingLayerName = "Pause HUD Cursor";
			meshText.transform.localScale = new Vector3(0.1f, 0.1f);
			g.transform.localScale = new Vector3(2.5f, 2.5f);
			g.transform.position = new Vector3(8.2f, 4.4f);
			imax = 40;
		}
		for(int i = 0; i < imax; i++) {
			if(g == null) { break; }
			g.transform.Rotate(0.0f, 0.0f, -3.0f);
			yield return new WaitForSeconds(0.01f);
		}
		SaveGeemu();
		for(int i = 0; i < imax; i++) {
			if(g == null) { break; }
			g.transform.Rotate(0.0f, 0.0f, -3.0f);
			yield return new WaitForSeconds(0.01f);
		}
		if(g != null) { Destroy(g); }
		if(meshText != null) { Destroy(meshText); }
		if(quitAtEnd) { Application.Quit(); }
	}

	private System.Collections.IEnumerator ChangeScreenInner(GS type) { yield return new WaitForSeconds(0.3f); currentScreen = type; Application.LoadLevel((int) type); }
	public void OnLevelWasLoaded() {
		isTransitioning = false;
		if(dontFade) { dontFade = false; return; }
		if(currentScreen == GS.Game) { InitGameObjectBank(); }
		StartFade(-1);
		SetupSound();
	}
	private void StartFade(int direction) { 
		holdFade = direction > 0;
		isFading = true; 
		fadeDir = direction; 
		fadeAlpha = fadeDir>0?0.0f:1.0f;
	}
	#endregion
	#region "Main Menu"
	public void MoveToDemo() {
		isDemo = true;
		isTutorial = false;
		rounds = 0; totalRoundTime = 0; totalP1RoundScore = 0; totalP2RoundScore = 0;
		gameType = GT.QuickPlay;
		useSpecial = Random.value > 0.7f;
		p1Char = (C) Random.Range(0, 10);
		p2Char = (C) Random.Range(0, 10);
		rowCount = Random.Range(4, 8);
		demoPlayers = (Random.value < 0.6f)?1:2;
		ChangeScreen(GS.Game);
	}
	public void MoveOutOfDemo() {
		p1Char = C.Null;
		p2Char = C.Null;
		rowCount = 6;
		GoToMainMenu();
	}
	public void MainMenuConfirmation(GT t) { gameType = t; level = 0; runningScore = 0; ChangeScreen(GetMenuNextState()); }
	private GS GetMenuNextState() {
		switch(gameType) {
			case GT.PlayerData: return GS.PlayerData;
			case GT.QuickPlay: return GS.CharSel;
			case GT.Arcade: return GS.CharSel;
			case GT.Campaign: return GS.CharSel;
			case GT.Versus: return GS.CharSel;
			case GT.Training: return GS.CharSel;
			case GT.Challenge: return GS.PuzSel;
			case GT.Options: return GS.Options;
		}
		return 0;
	}
	#endregion
	#region "Sound"
	public void SetupSound() {
		sounds = new GameObject("AudioContainers").AddComponent<AudioContainerContainer>();
		sounds.Init(saveInfo.savedOptions["vol_m"] / 100.0f, saveInfo.savedOptions["vol_s"] / 100.0f, saveInfo.savedOptions["vol_v"] / 100.0f, voicePitch, currentScreen == GS.Game);
		if(currentScreen == GS.Options || currentScreen == GS.PlayerData || currentScreen == GS.PuzSel || currentScreen == GS.CharSel) {
			sounds.SetMusicAndPlay(SoundPaths.M_Menu);
		} else if(currentScreen == GS.CutScene) {
			sounds.SetMusicAndPlay(SoundPaths.M_Cutscene);
			sounds.HalveMusicVolume();
		} else if(currentScreen == GS.Credits) {
			sounds.SetMusicAndPlay(SoundPaths.M_Credits, false);
		}
	}
	public void FadeMusic(string path) { sounds.FadeToMusicAndPlay(path); }
	public void AlterSound() { sounds.SetPitchP2(); }
	public float voicePitch = 1.0f;
	public void InhaleHelium() { if(voicePitch == 1.0f) { voicePitch = 1.5f; } else { voicePitch = 1.0f; } }
	#endregion
	#region "Character Select"
	public string GetPlayerSpritePathFromInt(int i, bool isBackground = false) { return GetPlayerSpritePath((C)i, isBackground); }
	public void SetPlayer1(int i, bool anotherEasterEgg = false) { 
		p1Char = (C)i;
		if(anotherEasterEgg) {
			p1Char = C.FuckingBalloon;
			balloonType = Random.Range(0, 3);
		} else {
			saveInfo.incrementCharacterFrequency(GetPlayerSpritePath(p1Char));
		}
	}
	public void SetPlayer2(int i) { p2Char = (C)i; }
	public string GetPlayerName(C p) { return System.Enum.GetName(typeof(C), p); }
	public int GetPlayerSpriteStartIdx(C p) { 
		switch(p) {
			case C.AliceAna: return 9;
			case C.Andrew: return 4; 
			case C.Depeche: return 6;
			case C.Devin: return 2;
			case C.George: return 0;
			case C.Joan: return 5;
			case C.Laila: return 8;
			case C.Lars: return 7;
			case C.Milo: return 1;
			case C.MJ: return 3;
			case C.September: return 11;
			case C.White: return 10;
		}
		return Random.Range(0, 12);
	}
	public string GetPlayerSpritePath(C p, bool isBackground = false, bool isMusic = false) { 
		switch(p) {
			case C.AliceAna: return "AliceAna";
			case C.Andrew: return "Andrew"; 
			case C.Depeche: return "Depeche";
			case C.Devin: return "Devin";
			case C.George: return "George";
			case C.Joan: return "Joan";
			case C.Laila: return "Laila";
			case C.Lars: return "Lars";
			case C.Milo: return "Milo";
			case C.MJ: return "MJ";
			case C.September: return isMusic?"White":"September";
			case C.White: return "White";
			case C.FuckingBalloon: return "MasterAlchemist";
			case C.Everyone: return "Everyone";
		}
		return isBackground?"Default":"George";
	}
	public string GetPlayerDisplayName(string p) { 
		switch(p) {
			case "AliceAna": return "Alice/Ana";
			case "Depeche": return "MODE";
			case "MJ": return "M.J.";
		}
		return p;
	}
	public void CharacterSelectConfirmation(bool moveToBalloon = false) {
		runningScore = 0; runningTime = 0;
		if(moveToBalloon) { MoveToBalloonBattle(); return; }
		if(gameType == GT.QuickPlay && p2Char == C.Null) {
			int justInCaseTheUnlikelyHappensAndThisTakesTooLong = 0;
			while((p2Char == p1Char || p2Char == C.Null) && justInCaseTheUnlikelyHappensAndThisTakesTooLong++ < 10) { p2Char = (C) Random.Range(0, 10); }
		}
		if(gameType == GT.Arcade) {
			GetNextOpponent();
			StartCoroutine(ChangeScreenAndSave(GS.CutScene));
		} else {
			StartCoroutine(ChangeScreenAndSave(GS.Game));
		}
	}
	#endregion
	#region "Arcade Mode"
	public void GetNextOpponent() {
		bool isBossChar = p1Char == C.White || p1Char == C.September;
		if(!isBossChar && level == 5) { p2Char = C.White; return; }
		if(!isBossChar && level >= 6) { p2Char = C.September; return; }
		C[] c;
		switch(p1Char) {
			case C.George: c = new C[] {C.Joan, C.Lars, C.Devin, C.Laila, C.Depeche}; break;
			case C.AliceAna: c = new C[] {C.Lars, C.Joan, C.Milo, C.Devin, C.Andrew}; break;
			case C.Devin: c = new C[] {C.MJ, C.Lars, C.Laila, C.AliceAna, C.Milo}; break;
			case C.Lars: c = new C[] {C.Depeche, C.MJ, C.Andrew, C.Devin, C.Laila}; break;
			case C.Andrew: c = new C[] {C.MJ, C.Depeche, C.George, C.Joan, C.AliceAna}; break;
			case C.Joan: c = new C[] {C.Laila, C.Milo, C.George, C.Depeche, C.MJ}; break;
			case C.Depeche: c = new C[] {C.AliceAna, C.Milo, C.Laila, C.Andrew, C.George}; break;
			case C.Milo: c = new C[] {C.George, C.AliceAna, C.Joan, C.Andrew, C.Devin}; break;
			case C.Laila: c = new C[] {C.MJ, C.Depeche, C.Devin, C.AliceAna, C.Lars}; break;
			case C.MJ: c = new C[] {C.Andrew, C.Lars, C.George, C.Milo, C.Joan}; break;
			default: c = new C[] {C.George, C.Milo, C.Devin, C.MJ, C.Andrew, C.Joan, C.Depeche, C.Lars, C.Laila, C.AliceAna}; break;
		}
		p2Char = c[level];
	}
	#endregion
	#region "Game"
	public void MoveToBalloonBattle() {
		gameType = GT.Arcade;
		rowCount = 6;
		rowCount2 = 6;
		isTutorial = false;
		aboutToFightAFuckingBalloon = false;
		balloonType = Random.Range(0, 3);
		p2Char = C.FuckingBalloon;
		difficulty = 13;
		ChangeScreen(GS.Game);
	}
	public void MoveFromHighScoreScreen() {
		runningScore = 0;
		level = 0;
		if(aboutToFightAFuckingBalloon) {
			MoveToBalloonBattle();
		} else {
			ChangeScreen(GS.CharSel);
		}
	}
	public void MoveFromWinScreen() { winType = 0; ChangeScreen(GS.HighScore); }
	public int GetScore(int depth, int length, float bonus, int d = -1) { return Mathf.FloorToInt(((d<0?difficulty:d) * 0.25f) * bonus * (depth * (depth>1?150:100) + length * 10)); }

	private bool AreAdditionalMatchesAreRedundant() {
		int p1Wins = 0, p2Wins = 0, halfRounds = Mathf.FloorToInt(rounds/2.0f);
		for(int i = 0; i < playerOneWonRound.Count; i++) { if(playerOneWonRound[i]) { p1Wins++; } else { p2Wins++; } }
		return (p1Wins > halfRounds || p2Wins > halfRounds);
	}
	public void DoWin(int score, int time, bool lost, bool updateData = true) {
		if(isTransitioning) { return; }
		if(updateData) { runningTime = time; runningScore = score; }
		won = !lost;
		if(won) { UpdateWinStats(); }
		if(p2Char == C.FuckingBalloon) {
			if(won) {
				saveInfo.savedOptions["beatafuckingballoon"] = 1;
				SetAchievement("STORY_MASTER");
				if(initialDifficulty >= 5) { SetAchievement("NEW_MASTER"); }
			}
			saveInfo.addPlayTime(gameType, runningTime);
			if(winType > 0) {
				won = true;
				int prevComplet = saveInfo.CalculateGameCompletionPercent();
				saveInfo.saveArcadeVictory(name, winType);
				SetArcadeWinAchivements(won, initialDifficulty);
				int newComplet = saveInfo.CalculateGameCompletionPercent();
				if(prevComplet < 50 && newComplet >= 50) {
					unlockNew = 1;
				} else if(prevComplet < 100 && newComplet == 100) {
					unlockNew = 2;
				}
			}
			StartCoroutine(ChangeScreenAndSave(GS.MainMenu));
			return;
		}
		if(rounds > 0) {
			totalRoundTime += time;
			bool endGame = false;
			playerOneWonRound.Add(won);
			if(won) { playerRoundTimes.Add(time); playerRoundScores.Add(score); }
			if(++currentRound <= rounds) {
				runningScore = 0;
				runningTime = 0;
				endGame = AreAdditionalMatchesAreRedundant();
				if(!endGame) { SaveAndReset(time); return; }
			}
			if(endGame || currentRound > rounds) {
				playerRoundTimes.Sort();
				playerRoundScores.Sort();
				runningTime = (playerRoundTimes.Count > 0) ? playerRoundTimes[0] : 0;
				runningScore = (playerRoundScores.Count > 0) ? playerRoundScores[playerRoundScores.Count - 1] : 0;
				if(rounds > 1) { StartCoroutine(ChangeScreenAndSave(GS.RoundWinner)); return; }
			}
		}
		bool advanceToWinScreenFromPuzzleScreen = false;
		if(gameType == GT.QuickPlay || gameType == GT.Campaign) {
			saveInfo.addPlayTime(gameType, runningTime);
			if(gameType == GT.Campaign) {
				if(time >= 900) { // 15 minutes
					SetAchievement("DOUG_FRIEND");
				}
				SetEndlessScores();
			}
			StartCoroutine(ChangeScreenAndSave(GS.HighScore));
			return;
		} else if(gameType == GT.Challenge) {
			int prevComplet = saveInfo.CalculateGameCompletionPercent();
			if(won) {
				saveInfo.addToPuzzles(level, runningScore, runningTime);
				if(saveInfo.getPuzzlesCompleted() == 32) {
					SetAchievement("PUZZLE");
				}
			}
			int newComplet = saveInfo.CalculateGameCompletionPercent();
			if(prevComplet < 50 && newComplet >= 50) {
				unlockNew = 1;
				advanceToWinScreenFromPuzzleScreen = true;
			} else if(prevComplet < 100 && newComplet == 100) {
				unlockNew = 2;
				advanceToWinScreenFromPuzzleScreen = true;
			}
		} 
		if(gameType != GT.Arcade) {
			saveInfo.addPlayTime(gameType, runningTime);
			runningScore = 0;
			runningTime = 0;
			if(gameType == GT.Challenge && advanceToWinScreenFromPuzzleScreen) {
				p1Char = unlockNew == 1 ? C.White : C.September;
				p2Char = p1Char;
				StartCoroutine(ChangeScreenAndSave(GS.WinnerIsYou)); 
				return;
			}
			GS nextScreen = gameType==GT.Challenge?GS.PuzSel:GS.CharSel;
			StartCoroutine(ChangeScreenAndSave(nextScreen)); 
			return;
		}
		if(lost) {
			saveInfo.addPlayTime(gameType, runningTime);
			string name = GetPlayerSpritePath(p1Char);
			if(winType > 0) { 
				won = true;
				int prevComplet = saveInfo.CalculateGameCompletionPercent();
				saveInfo.saveArcadeVictory(name, winType);
				SetArcadeWinAchivements(won, initialDifficulty);
				int newComplet = saveInfo.CalculateGameCompletionPercent();
				if(prevComplet < 50 && newComplet >= 50) {
					unlockNew = 1;
				} else if(prevComplet < 100 && newComplet == 100) {
					unlockNew = 2;
				}
			}
			GS nextScreen = won?GS.WinnerIsYou:GS.HighScore;
			StartCoroutine(ChangeScreenAndSave(nextScreen));
			return;
		}
		int dragonScore = 100 * GetScore(2, 5, 1.0f, initialDifficulty), puhLoonScore = (int) (dragonScore * 2.8);
		if(difficulty < 3)  { puhLoonScore *= 3; } if(difficulty < 7)  { puhLoonScore *= 3; }
		if(p1Char == C.FuckingBalloon) { dragonScore = runningScore + 100; puhLoonScore = runningScore + 100; }
		if(level % 2 == 0) { difficulty++; }
		if(p1Char == C.White || p1Char == C.September) {
			if(level == 9) {
				winType = 2;
				saveInfo.addPlayTime(gameType, runningTime);
				string name = GetPlayerSpritePath(p1Char);
				saveInfo.saveArcadeVictory(name, winType);
				SetArcadeWinAchivements(won, initialDifficulty);
				StartCoroutine(ChangeScreenAndSave(GS.WinnerIsYou));
			} else {
				level++;
				ChangeScreen(GS.CutScene);
			}
		} else {
			if(level == 7 && runningScore >= puhLoonScore) { aboutToFightAFuckingBalloon = true; level++; }
			else if(level == 5 && runningScore >= dragonScore) {
				level = 7;
				difficulty++;
			} else { level++; }
			ChangeScreen(GS.CutScene);
		}
	}
	#endregion
	#region "Saving/Loading/Options"
	public bool IsFirstTime() { return saveInfo.firstTime; }
	public void SaveGeemu() { saveInfo.Save(); }
	public void LoadGeemu() { 
		if(saveInfo == null) {
			SaveIOCore s = new SaveIO_PC();
			saveInfo = new SaveData(s);
		} 
		SaveData res = saveInfo.Load();
		if(res != null) { saveInfo = res; saveInfo.ApplyPatch(); }
	}
	public void WipeData() { saveInfo.EraseSaveData(); saveInfo.Save(); }
	public SaveData GetSaveData() { return saveInfo; }
	public bool UseHighContrastCursor() { return saveInfo.savedOptions["emphasizecursor"] == 1; }
	public bool IsColorBlind() { return saveInfo.savedOptions["colorblind"] == 1; }
	public bool IsScopophobic() { return saveInfo.savedOptions["scopophobia"] == 1; }
	public bool IsLeftAlignedHUD() { return saveInfo.savedOptions["hudplacement"] == 0; }
	public void SetOption(string s, int v) { if(!saveInfo.savedOptions.ContainsKey(s)) { saveInfo.savedOptions.Add(s, v); } else { saveInfo.savedOptions[s] = v; } }
	public void SetRes() { Screen.SetResolution(saveInfo.savedOptions["width"], saveInfo.savedOptions["height"], saveInfo.savedOptions["fullscreen"] == 1); }
	public void StoreName(string n) { if(n.Length != 3) { return; } saveInfo.highScoreName = n; }
	public void StoreScore(string n, int s) { saveInfo.addToHighScore(n, s, gameType); }
	public void StoreTime(string n, int s) { saveInfo.addToTime(n, s, gameType); }
	public void SetCharSelOptionVal(string key, int val) { saveInfo.gameOptionDefaults[key] = val; }
	public int GetCharSelOptionVal(string key) {
		if(saveInfo.gameOptionDefaults == null) { saveInfo.setupGameOptionDefaults(); StartCoroutine(SameScreenSave()); }
		return saveInfo.gameOptionDefaults[key];
	}
	public void SetKeyBinding(int player, int key, string val) {
		if(player == 0) {
			if(usingGamepad1) {
				saveInfo.controlBindingsGamepadP1[key] = val;
			} else {
				saveInfo.controlBindingsP1[key] = val;
			}
		} else {
			if(usingGamepad2) {
				saveInfo.controlBindingsGamepadP2[key] = val;
			} else {
				saveInfo.controlBindingsP2[key] = val;
			}
		}
		StartCoroutine(SameScreenSave());
	}
	public bool IsKeyInUse(int key) {
		string skey = key.ToString();
		foreach(string v in saveInfo.controlBindingsP1.Values) { if(v == skey) { return true; } }
		return false;
	}
	public Dictionary<int, string> GetKeyBindings(int player = 0) {
		if(saveInfo.controlBindingsP1 == null) { saveInfo.SetupDefaultKeyControls(); StartCoroutine(SameScreenSave()); }
		if(saveInfo.controlBindingsGamepadP1 == null) { saveInfo.SetupDefaultPadControls(); StartCoroutine(SameScreenSave()); }
		if(player == 0) {
			return usingGamepad1 ? saveInfo.controlBindingsGamepadP1 : saveInfo.controlBindingsP1;
		} else {
			return usingGamepad2 ? saveInfo.controlBindingsGamepadP2 : saveInfo.controlBindingsP2;
		}
	}
	#endregion
	#region "Puzzles"
	public int GetPuzzleLevel() { return level; }
	public void SetPuzzleLevel(int i) { level = i; }
	public void LowerPuzzleLevel() { level--; }
	#endregion
}