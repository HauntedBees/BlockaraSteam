using DG.Tweening;
using UnityEngine;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
public class OnlineHelper:All {
	protected Callback<UserStatsReceived_t> Callback_statsReceived;
	protected Callback<LobbyCreated_t> Callback_lobbyCreated;
	protected Callback<LobbyMatchList_t> Callback_lobbyList;
	protected Callback<LobbyEnter_t> Callback_lobbyEnter;
	protected Callback<LobbyDataUpdate_t> Callback_lobbyInfo;
	protected Callback<P2PSessionRequest_t> _p2PSessionRequestCallback;
	ulong current_lobbyID;
	List<CSteamID> lobbyIDS;
	public TextMesh myMesh;
	private int myMode, offsetY;
	public int initializedStatus;
	private XmlNode top;
	private MenuCursor lobbyCursor;
	private CharSelectController parent;
	private float lastTimeMessageReceived = 0.0f;
	private TextMesh player1Rank, player2Rank;

	private List<KeyValuePair<string, CSteamID>> lobbyDisplay;
	private void LobbyUpdate() {
		MoveTextUp();
		if(lobbyCursor.launchOrPause()) {
			int idx = offsetY + (8 - lobbyCursor.getY());
			if(idx >= lobbyDisplay.Count) {
				idx = lobbyDisplay.Count - 1;
			}
			if(idx == 1) {
				FindFriendLobbies();
			} else if(idx == 0) {
				initializedStatus = -3;
			} else {
				CSteamID lobby = lobbyDisplay[idx].Value;
				SetText("joining");
				SteamAPICall_t try_joinLobby = SteamMatchmaking.JoinLobby(lobby);
			}
			return;
		} else if(lobbyCursor.back()) {
			initializedStatus = -3;
			return;
		}

		string res = "";
		int actOffset = offsetY;
		if(offsetY > 0) {
			res = "...\r\n";
			actOffset++;
		}
		int max = lobbyDisplay.Count;
		bool addEnd = false;
		if((actOffset + 8) < max) {
			max = actOffset + 7;
			addEnd = true;
		}
		for(int i = actOffset; i < max; i++) {
			res += lobbyDisplay[i].Key + "\r\n";
		}
		if(addEnd) { res += "..."; }
		ForceText(res);
		lobbyCursor.SetVisibility(true);
		lobbyCursor.DoUpdate();
		int maxY = Mathf.Max(9 - lobbyDisplay.Count, 0);
		int curY = lobbyCursor.getY();
		if(curY < maxY) {
			lobbyCursor.setY(maxY);
		} else if(curY == 0 && addEnd) {
			offsetY++;
			lobbyCursor.setY(1);
		} else if(curY == 8 && offsetY > 0) {
			offsetY--;
			lobbyCursor.setY(7);
		}
	}
	private void MoveTextUp() { myMesh.transform.position = new Vector3(2.625f, 1.9f); }
	private void MoveTextDown() { myMesh.transform.position = new Vector3(2.5f, 1.1f); }


	public void Start() {
		lobbyIDS = new List<CSteamID>();
		lobbyDisplay = new List<KeyValuePair<string, CSteamID>>();
		offsetY = 0;

		_p2PSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
		Callback_lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
		Callback_lobbyList = Callback<LobbyMatchList_t>.Create(OnGetLobbiesList);
		Callback_lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
		Callback_lobbyInfo = Callback<LobbyDataUpdate_t>.Create(OnGetLobbyInfo);
		Callback_statsReceived = Callback<UserStatsReceived_t>.Create(OnGetUserStats);

		initializedStatus = 0;


		if (SteamAPI.Init()) {
			initializedStatus = 1;
		} else {
			initializedStatus = -1;
		}
	}

	public void OnGetUserStats(UserStatsReceived_t pCallback) {
		if(pCallback.m_steamIDUser == SteamUser.GetSteamID()) {
			int score = 0;
			SteamUserStats.GetStat("GAMESCORE", out score);
			player1Rank.text = SteamFriends.GetPersonaName() + " (" + score + ")";
		} else if(pCallback.m_steamIDUser == otherUsr) {
			int score = 0;
			SteamUserStats.GetUserStat(otherUsr, "GAMESCORE", out score);
			player2Rank.text = SteamFriends.GetFriendPersonaName(otherUsr) + " (" + score + ")";
		}
	}

	public void Setup(int mode, TextMesh infoHolder, MenuCursor lc, CharSelectController csc) {
		GetPersistData();
		top = GetXMLHead();
		myMesh = infoHolder;
		myMode = mode;
		lobbyCursor = lc;
		myMesh.text = GetXmlValue(top, "connecting");
		parent = csc;
		FontData f = PD.mostCommonFont.Clone();
		f.color = Color.white;
		player1Rank = GetMeshText(new Vector3(-0.745f, 1.9f), SteamFriends.GetPersonaName(), f);
		SteamUserStats.RequestCurrentStats();
	}
	public void Refresh(int mode) {
		myMode = mode;
		myMesh.text = GetXmlValue(top, "connecting");

		lobbyIDS.Clear();
		lobbyDisplay.Clear();
		offsetY = 0;
		initializedStatus = 0;
		
		if (SteamAPI.Init()) {
			initializedStatus = 1;
		} else {
			initializedStatus = -1;
		}

	}
	private void SetText(string id) {
		try {
			myMesh.text = GetXmlValue(top, id).Replace("{0}","\r\n");
		} catch(System.Exception e) {
			Debug.Log ("WUH: " + e.Message);
		}
	}
	private void ForceText(string t) { myMesh.text = t; }

	public void Update() {
		SteamAPI.RunCallbacks();
		if(initializedStatus == -3) { // LEAVING LOBBY LIST
			MoveTextDown();
			lobbyCursor.SetVisibility(false);
			lobbyCursor.setY(8);
			SetText("leftlobby");
			initializedStatus = -2;
		} else if(initializedStatus == -1) { // CONNECTION FAILED
			SetText("connfail");
			initializedStatus = -2;
		} else if(initializedStatus == 1) { // SELECTING OPTION
			if(myMode == 1) {
				SetText("roomMake");
				SteamAPICall_t try_toHost = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 2);
				initializedStatus = 3;
			} else if(myMode == 2) {
				SetText("roomFind");
				FindFriendLobbies();
			} else if(myMode == 3) {
				SetText("matchFind");
				SteamAPICall_t try_getList = SteamMatchmaking.RequestLobbyList();
			}
		} else if(initializedStatus == 2) { // VIEWING FRIEND LIST
			LobbyUpdate();
		} else if(initializedStatus == 4) { // HOST WAITING FOR FRIEND TO JOIN
			HandleWaiting();
		} else if(initializedStatus == 5) { // FRIEND WAITING FOR BOTH TO BE READY
			if(SteamMatchmaking.GetNumLobbyMembers((CSteamID) current_lobbyID) < 2) { // whoppy machine broke
				BailFromState5(false);
			} else if(lobbyCursor.back()) { // bailing
				BailFromState5(false);
				return;
			} else if(lobbyCursor.launchOrPause()) {
				p1Ok.GetComponent<SpriteRenderer>().sprite = roundsSheet[1];
				SteamMatchmaking.SetLobbyMemberData((CSteamID)current_lobbyID, "ready", "yes");
				isReady = true;
			}
			if(SteamMatchmaking.GetLobbyData((CSteamID)current_lobbyID, "ready") == "yes") {
				p2Ok.GetComponent<SpriteRenderer>().sprite = roundsSheet[1];
				lastTimeMessageReceived = Time.time;
				if(isReady) {
					CleanUpReadyButtons(true);
					initializedStatus = 7;
				}
			} else if(IsTimedOut(20.0f)) {
				BailFromState5();
				return;
			}
		} else if(initializedStatus == 6) { // HOST WAITING FOR BOTH TO BE READY
			if(SteamMatchmaking.GetNumLobbyMembers((CSteamID) current_lobbyID) < 2) { // whoppy machine broke
				parent.HidePlayer2();
				SetText("hostfriendlefthost");
				Destroy(player2Rank);
				myMesh.gameObject.SetActive(true);
				SteamMatchmaking.SetLobbyData((CSteamID) current_lobbyID, "status", "open");
				initializedStatus = 4;
				CleanUpReadyButtons(false);
			} else if(lobbyCursor.back()) { // bailing
				parent.HidePlayer2();
				SetText("friendlefthost");
				MoveTextDown();
				myMesh.gameObject.SetActive(true);
				SteamMatchmaking.SetLobbyData((CSteamID)current_lobbyID, "status", "dead");
				SteamMatchmaking.LeaveLobby((CSteamID)current_lobbyID);
				CleanUpReadyButtons(false);
				Destroy(player2Rank);
				initializedStatus = -2;
				return;
			} else if(lobbyCursor.launchOrPause()) {
				p1Ok.GetComponent<SpriteRenderer>().sprite = roundsSheet[1];
				SteamMatchmaking.SetLobbyData((CSteamID)current_lobbyID, "ready", "yes");
				isReady = true;
			}
			if(SteamMatchmaking.GetLobbyMemberData((CSteamID)current_lobbyID, otherUsr, "ready") == "yes") {
				p2Ok.GetComponent<SpriteRenderer>().sprite = roundsSheet[1];
				if(isReady) {
					CleanUpReadyButtons(true);
					initializedStatus = 8;
				}
			} else if(IsTimedOut(20.0f)) {
				BailFromState5();
				return;
			}
		} else if(initializedStatus == 7) { // p2 waiting for P2P connection and match starting *
			PD.isOnlineHost = false;
			PD.onlineOpponentId = (ulong) otherUsr;
			//Debug.Log ("AWAITING P2P CONNECTION");
			if(GetMessages().Count > 0) {
				lastTimeMessageReceived = Time.time;
				SteamMatchmaking.SetLobbyMemberData((CSteamID) current_lobbyID, "fullReady", "yes");
				SendMessage();
				GoToMatch();
			} else if(IsTimedOut(20.0f)) {
				BailFromState5();
				return;
			}
			// waiting for OnP2PSessionRequest
		} else if(initializedStatus == 8) { // p1 sending P2P connection
			SendMessage();
			Debug.Log ("SENDING P2P CONNECTION TO " + otherUsr.ToString());
			initializedStatus = 9;
			if(IsTimedOut(20.0f)) {
				BailFromState5();
				return;
			}
		} else if(initializedStatus == 9) { // p1 waiting for p2 to receive
			//if(SteamMatchmaking.GetLobbyMemberData((CSteamID)current_lobbyID, otherUsr, "fullReady") == "yes") {
			Debug.Log ("AWAITING P2P CONNECTION");
			PD.isOnlineHost = true;
			PD.onlineOpponentId = (ulong) otherUsr;
			if(GetMessages().Count > 0) {
				lastTimeMessageReceived = Time.time;
				SteamMatchmaking.SetLobbyMemberData((CSteamID) current_lobbyID, "fullReady", "yes");
				GoToMatch();
			} else if(IsTimedOut(20.0f)) {
				BailFromState5();
				return;
			}
		}
	}
	private void BailFromState5(bool timeout = true) {
		parent.HidePlayer2();
		SetText("friendlefthost");
		myMesh.gameObject.SetActive(true);
		Destroy (player2Rank);
		if(timeout) { SetText("timeout"); }
		SteamMatchmaking.SetLobbyData((CSteamID)current_lobbyID, "status", "open");
		SteamMatchmaking.LeaveLobby((CSteamID)current_lobbyID);
		initializedStatus = 1;
		CleanUpReadyButtons(false);
		isReady = false;
		otherUsr = (CSteamID)0;
		current_lobbyID = 0;
	}


	private bool IsTimedOut(float timeAllowed = 10.0f) { return (Time.time - lastTimeMessageReceived) >= timeAllowed; }

	private void SendMessage() {
		string hello = "Hello!";
		byte[] bytes = new byte[hello.Length * sizeof(char)];
		using (System.IO.MemoryStream ms = new System.IO.MemoryStream(bytes)) {
			using (System.IO.BinaryWriter b = new System.IO.BinaryWriter(ms)) {
				b.Write(hello);
			}
		}
		SteamNetworking.SendP2PPacket(otherUsr, bytes, (uint) bytes.Length, EP2PSend.k_EP2PSendReliable);
	}

	private bool isReady = false;
	private List<string> GetMessages() {
		uint size;
		List<string> m = new List<string>();
		while (SteamNetworking.IsP2PPacketAvailable(out size))
		{
			byte[] buffer = new byte[size];
			uint bytesRead;
			CSteamID remoteId;
			if (SteamNetworking.ReadP2PPacket(buffer, size, out bytesRead, out remoteId)) {
				using (System.IO.MemoryStream ms = new System.IO.MemoryStream(buffer)) {
					using (System.IO.BinaryReader b = new System.IO.BinaryReader(ms)) {
						string message = b.ReadString();
						m.Add(message);
						//Debug.Log("Received a message: " + message);
					}
				}
			}
		}
		return m;
	}

	private void OnP2PSessionRequest(P2PSessionRequest_t request) {
		Debug.Log ("RECEIVED P2P CONNECTION");
		CSteamID clientId = request.m_steamIDRemote;
		if (clientId == otherUsr) {
			Debug.Log ("RECIEVED CORRECT P2P CONNECTION");
			SteamNetworking.AcceptP2PSessionWithUser(clientId);
			SteamMatchmaking.SetLobbyMemberData((CSteamID) current_lobbyID, "fullReady", "yes");
			lastTimeMessageReceived = Time.time;
			if(!PD.isOnlineHost) {
				SendMessage();
			}
			GoToMatch();
		}
	}
	private void GoToMatch() {
		parent.ForceTransition();
		PD.MoveToOnlineBattle();
	}

	private void FindFriendLobbies() {
		lobbyDisplay.Clear();
		lobbyIDS.Clear();
		lobbyDisplay.Add(new KeyValuePair<string, CSteamID>("Back", (CSteamID) 0));
		lobbyDisplay.Add(new KeyValuePair<string, CSteamID>("Refresh", (CSteamID) 0));
		initializedStatus = 2;

		int steamFriends = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
		for(int i = 0; i < steamFriends; i++) {
			FriendGameInfo_t friendGameInfo;
			CSteamID steamIDFriend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
			if(SteamFriends.GetFriendGamePlayed(steamIDFriend, out friendGameInfo) && friendGameInfo.m_steamIDLobby.IsValid()) {
				lobbyIDS.Add(friendGameInfo.m_steamIDLobby);
				SteamMatchmaking.RequestLobbyData(friendGameInfo.m_steamIDLobby);
			}
		}
	}
	void OnGetLobbyInfo(LobbyDataUpdate_t result) {
		for(int i=0; i < lobbyIDS.Count; i++) {
			if (lobbyIDS[i].m_SteamID == result.m_ulSteamIDLobby) {
				CSteamID id = (CSteamID)lobbyIDS[i].m_SteamID;
				if(SteamMatchmaking.GetLobbyData(id, "status") == "open") {
					lobbyDisplay.Add(new KeyValuePair<string, CSteamID>(SteamMatchmaking.GetLobbyData(id, "name"), id));
				}
				return;
			}
		}
	}


	void OnLobbyCreated(LobbyCreated_t result) {
		if (result.m_eResult == EResult.k_EResultOK) {
			SetText("roomMakeSucc");
			current_lobbyID = result.m_ulSteamIDLobby;
			initializedStatus = 4;
		} else {
			SetText("roomMakeFail");
			initializedStatus = -2;
		}
		string personalName = SteamFriends.GetPersonaName();
		CSteamID id = (CSteamID)result.m_ulSteamIDLobby;
		SteamMatchmaking.SetLobbyData(id, "name", personalName);
		SteamMatchmaking.SetLobbyData(id, "status", "open");
		SteamMatchmaking.SetLobbyData(id, "playerone", ((int) PD.p1Char).ToString());
		SteamMatchmaking.SetLobbyData(id, "playertwo", "");
	}
	
	void OnGetLobbiesList(LobbyMatchList_t result) {
		for(int i = 0; i< result.m_nLobbiesMatching; i++) {
			CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
			lobbyIDS.Add(lobbyID);
			SteamMatchmaking.RequestLobbyData(lobbyID);
		}
	}

	private GameObject begin, beginText, cancel, cancelText, p1Ok, p2Ok;
	private Sprite[] roundsSheet;
	private bool alreadyInitializedReadyButtons = false;
	private void SetUpReadyButtons() {
		if(alreadyInitializedReadyButtons) { return; }
		alreadyInitializedReadyButtons = true;
		parent.ToggleDisplayOptions(false);
		FontData f = PD.mostCommonFont.Clone(); f.scale = 0.03f;
		Sprite[] beginSheet = Resources.LoadAll<Sprite>(SpritePaths.LongButtons);
		begin = GetGameObject(new Vector3(0.0f, 0.62f), "Ready", beginSheet[0], true, "HUD");
		beginText = GetMeshText(new Vector3(0.0f, 0.715f), string.Format(GetXmlValue(top, "ready"), PD.GetP1InputName(InputMethod.KeyBinding.launch)), f).gameObject;

		cancel = GetGameObject(new Vector3(0.0f, 0.3f), "Cancel", beginSheet[0], true, "HUD");
		cancelText = GetMeshText(new Vector3(0.0f, 0.401f), string.Format(GetXmlValue(top, "onlinecancel"), PD.GetP1InputName(InputMethod.KeyBinding.back)), f).gameObject;

		roundsSheet = Resources.LoadAll<Sprite>(SpritePaths.RoundStateIcons);
		
		p1Ok = GetGameObject(new Vector3(-0.25f, 0.88f), "P1 Ready", roundsSheet[2], true, "HUD");
		p2Ok = GetGameObject(new Vector3(0.25f, 0.88f), "P2 Ready", roundsSheet[2], true, "HUD");

		parent.chooseText.SetActive(false);
	}
	private GameObject loadGuy;
	private void CleanUpReadyButtons(bool toNext) {
		alreadyInitializedReadyButtons = false;
		Destroy (begin);
		Destroy(beginText);
		Destroy (cancel);
		Destroy(cancelText);
		Destroy (p1Ok);
		Destroy(p2Ok);
		if(toNext) {
			loadGuy = GetGameObject(new Vector3(0f, 1f), "Loading", Resources.Load<Sprite>(SpritePaths.Saving));
			loadGuy.transform.DORotate(new Vector3(0f, 0f, -360f), 2f, RotateMode.FastBeyond360).SetLoops(-1).SetEase(Ease.Linear);
		} else {
			parent.ToggleDisplayOptions(true);
		}
	}

	private CSteamID otherUsr;
	private void HandleWaiting() {
		if(lobbyCursor.back()) {
			SetText("lefthost");
			myMesh.gameObject.SetActive(true);
			SteamMatchmaking.LeaveLobby((CSteamID)current_lobbyID);
			initializedStatus = -2;
			current_lobbyID = 0;
			return;
		}
		int numusers = SteamMatchmaking.GetNumLobbyMembers((CSteamID) current_lobbyID);
		if(numusers == 1) {
			return;
		}
		SteamMatchmaking.SetLobbyData((CSteamID)current_lobbyID, "status", "full");
		SetUpReadyButtons();
		otherUsr = SteamMatchmaking.GetLobbyMemberByIndex((CSteamID)current_lobbyID, 1);
		lastTimeMessageReceived = Time.time;

		int score = 0;
		SteamUserStats.GetUserStat(otherUsr, "GAMESCORE", out score);

		if(player2Rank == null) {
			FontData f = PD.mostCommonFont.Clone(); f.color = Color.white;
			player2Rank = GetMeshText(new Vector3(0.745f, 1.8f), SteamFriends.GetFriendPersonaName(otherUsr), f);
			SteamUserStats.RequestUserStats(otherUsr);
		}

		string p2 = SteamMatchmaking.GetLobbyMemberData((CSteamID)current_lobbyID, otherUsr, "player");
		if(string.IsNullOrEmpty(p2)) { return; }
		parent.ForcePlayer2(int.Parse(p2));
		initializedStatus = 6;
		myMesh.gameObject.SetActive(false);
	}
	
	void OnLobbyEntered(LobbyEnter_t result) {
		if(current_lobbyID == result.m_ulSteamIDLobby) {
			return;
		}
		current_lobbyID = result.m_ulSteamIDLobby;
		if (result.m_EChatRoomEnterResponse == 1) {
			if(SteamMatchmaking.GetLobbyData((CSteamID)current_lobbyID, "status") != "open" || SteamMatchmaking.GetNumLobbyMembers((CSteamID) current_lobbyID) > 2) {
				SetText("joinfail");
				initializedStatus = -2;
				SteamMatchmaking.LeaveLobby((CSteamID)current_lobbyID);
				return;
			}
			otherUsr = SteamMatchmaking.GetLobbyMemberByIndex((CSteamID)current_lobbyID, 0);
			if((CSteamID)otherUsr == SteamUser.GetSteamID()) {
				Debug.Log("FUCK!");
				return;
			}
			initializedStatus = 5;
			SetUpReadyButtons();
			lastTimeMessageReceived = Time.time;
			//Debug.Log ("OTHERUSR: " + otherUsr.ToString());
			SteamMatchmaking.SetLobbyMemberData((CSteamID)current_lobbyID, "player", ((int) PD.p1Char).ToString());
			int score = 0;
			SteamUserStats.GetUserStat(otherUsr, "GAMESCORE", out score);
			if(player2Rank == null) {
				FontData f = PD.mostCommonFont.Clone(); f.color = Color.white;
				player2Rank = GetMeshText(new Vector3(0.745f, 1.8f), SteamFriends.GetFriendPersonaName(otherUsr), f);
				SteamUserStats.RequestUserStats(otherUsr);
			}
			parent.ForcePlayer2(int.Parse(SteamMatchmaking.GetLobbyData((CSteamID)current_lobbyID, "playerone")));
			myMesh.gameObject.SetActive(false);
		} else {
			SetText("joinfail");
			initializedStatus = -2;
			current_lobbyID = 0;
		}
	}
}