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
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Text;
public class OnlineGameController:GameController {
	private CSteamID opponent;
	private int onlineState;
	private float timeUntilProgression;
	private float lastTimeMessageReceived = 0.0f;
	protected override void DoTheActualSetup() {
		StateControllerInit(false);
		onlineState = 0;
		usingTouchControls = PD.GetSaveData().savedOptions["touchcontrols"] == 1;
		firstLaunch = true;
		player1Human = true;
		player2Human = false;
		opponent = (CSteamID) PD.onlineOpponentId;
		zaps = new List<ZappyGun>();
		zapsToDelete = new List<ZappyGun>();
		SetupActors();
		SetupRoundDisplay();
		SetupEasterEgg();
		specialMode = false;
		height = 6; width = 8;
		SetUpHUDAndScores();
		pauseButtonSheet = Resources.LoadAll<Sprite>(SpritePaths.ShortButtons);
		pauseButton = GetGameObject(player2Human ? (new Vector3(0.0f, -0.1f)):(new Vector3(2.5f, 0.7f)), "Pause Button", pauseButtonSheet[0], true, "HUD");
		pauseButton.SetActive(PD.usingMouse);
		pauseButton.transform.localScale = new Vector3(0.75f, 0.75f);
		FontData f = PD.mostCommonFont.Clone(); f.scale = 0.035f;
		pauseText = GetMeshText(new Vector3(2.5f, 0.8f), GetXmlValue(GetXMLHead(), "pause"), f).gameObject;
		pauseText.SetActive(PD.usingMouse);
		pauseTimer = 0;
		mouseObjects.Add(pauseButton);
		mouseObjects.Add(pauseText);
		timeUntilProgression = 5.0f;
		if(PD.isOnlineHost) {
			Debug.Log ("P1 SENDING SEED");
			onlineState = 1;
			int seed = Random.seed;
			FinishSetup(seed);
			SendMessage(seed.ToString());
		}
	}

	protected override bool ActualUpdate() {
		SteamAPI.RunCallbacks();
		if(onlineState == 0) { // P2 awaiting seed from P1
			List<string> ms = GetMessages();
			if(ms.Count == 0) { return true; }
			int seed = 0;
			if(!int.TryParse(ms[0], out seed)) {
				Debug.Log("unexpected first message: " + ms[0]);
				return true;
			}
			FinishSetup(seed);
			SendMessage("ok");
			SetupCountdown();
			onlineState = 2;
		} else if(onlineState == 1) { // P1 awaiting reply from P2
			List<string> ms = GetMessages();
			if(ms.Count == 0) { return true; }
			if(ms[0] == "ok") {
				SetupCountdown();
				onlineState = 2;
			} else {
				Debug.Log("unexpected reply: " + ms[0]);
			}
		} else if(onlineState == 2) { // ingame
			if(base.ActualUpdate()) {
				ReadMessages();
				if(medicShoutCooldownTime > 0f) {
					medicShoutCooldownTime -= Time.deltaTime;
					if(medicShoutCooldownTime <= 0f) {
						medicCountSinceRateLimit = 0;
					}
				}
				SerializeAction();
			}
			if((Time.time - lastTimeMessageReceived) > 10f) {
				PD.forceOnlinePause = true;
				HandlePause(true);
			}
		}
		return true;
	}

	private bool calledDoWin = false;
	protected override bool HandleGameOver() {
		if(!gameOver) { return false; }
		timeUntilProgression -= Time.deltaTime;
		if(timeUntilProgression <= 0 && !calledDoWin) {
			calledDoWin = true;
			Debug.Log(PD.currentRound + "/" + PD.rounds);
			PD.DoWin(board1.GetScore(), hud.GetTimeInSeconds(), board1.IsDead());
		}
		return true;
	}
	
	protected override void EasterEggsArentSoEasteryWhenTheCodeIsOpenSourceIsItYouFuckdummy() { 
		if(Input.GetKeyDown(medicKey)) {
			actor1.SayThingFromXML("082");
			SendMessage("MEDIC");
		}
	}

	private void ReadMessages() {
		List<string> ms = GetMessages();
		XmlSerializer xs = new XmlSerializer(typeof(AIAction));
		foreach(string m in ms) {
			if(m.StartsWith("AI:")) {
				((BoardCursorBot) cursor2).PushAction(m.Substring(3));
			} else if(m == "MEDIC") {
				OnlineMedicShout();
			}
		}
	}

	private int medicCountSinceRateLimit = 0;
	private float medicShoutCooldownTime = 0f;
	private void OnlineMedicShout() {
		if(++medicCountSinceRateLimit >= 3) { return; }
		medicShoutCooldownTime = 2.0f;
		actor2.SayThingFromXML("082");
	}

	private void SerializeAction() {
		AIAction o = new AIAction(cursor1.dx, cursor1.dy, board1.shifting, board1.launchInfo.launching, board1.shiftall);
		if(o.dx == 0 & o.dy == 0 && o.shift == 0 && !o.shiftall && !o.launch) {
			return;
		}
		if(o.shiftall) { o.dx -= o.shift; }
		XmlSerializer xs = new XmlSerializer(typeof(AIAction));
		using(System.IO.StringWriter sw = new System.IO.StringWriter()) {
			xs.Serialize(sw, o);
			SendMessage("AI:" + sw.ToString());
		}
	}

	private void SendMessage(string hello) {
		//Debug.Log ("SENDING " + hello);
		byte[] bytes = new byte[hello.Length * sizeof(char)];
		using (System.IO.MemoryStream ms = new System.IO.MemoryStream(bytes)) {
			using (System.IO.BinaryWriter b = new System.IO.BinaryWriter(ms)) {
				b.Write(hello);
			}
		}
		SteamNetworking.SendP2PPacket(opponent, bytes, (uint) bytes.Length, EP2PSend.k_EP2PSendReliable);
	}

	private List<string> GetMessages() {
		uint size;
		List<string> m = new List<string>();
		while (SteamNetworking.IsP2PPacketAvailable(out size)) {
			byte[] buffer = new byte[size];
			uint bytesRead;
			CSteamID remoteId;
			if (SteamNetworking.ReadP2PPacket(buffer, size, out bytesRead, out remoteId)) {
				lastTimeMessageReceived = Time.time;
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


	private void FinishSetup(int seed) {
		bh = new BlockHandler(PD, PD.GetPuzzleLevel(), true, seed);
		float p1Xoffset = (PD.IsLeftAlignedHUD()?-1.5f:-5.5f), p2Xoffset = 3.0f;
		CreateBoards(p1Xoffset, p2Xoffset);
		cursor1 = CreatePlayerCursor(player1Human, p1Xoffset, 1, board1, board2);
		cursor2 = CreatePlayerCursor(false, p2Xoffset, 2, board2, board1, PD.override2P);
		if(PD.isOnlineHost) {
			board1.Setup(cursor1, th, bh, new Vector2(PD.IsLeftAlignedHUD()?-0.725f:0.75f, -0.6f), false, true, player1Human && usingTouchControls);
			board2.Setup(cursor2, th, bh, new Vector2(0.2f, -0.6f), true, false);
		} else {
			board2.Setup(cursor2, th, bh, new Vector2(0.2f, -0.6f), true, false);
			board1.Setup(cursor1, th, bh, new Vector2(PD.IsLeftAlignedHUD()?-0.725f:0.75f, -0.6f), false, true, player1Human && usingTouchControls);
		}
		board1.RefreshGraphics();
		board2.RefreshGraphics();
		if(PD.runningScore > 0) { board1.AddToScore(PD.runningScore); }
		if(PD.runningTime > 0) { hud.SetTimeWithSeconds(PD.runningTime); }
		CreateMirrors(p1Xoffset, p2Xoffset);
		mirror1.RefreshGraphics();
		mirror2.RefreshGraphics();
		SetupMouseControls(p1Xoffset);
	}
}