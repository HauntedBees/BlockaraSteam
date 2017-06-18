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
public class MatchOverController:CharDisplayController {
	protected Callback<UserStatsReceived_t> Callback_statsReceived;
	private ParticleSystem particles;
	private ParticleSystem.Particle[] pars;
	private CutsceneChar winner, loser;
	private int applauseTimer;
	private GameObject begin, beginText, cancel, cancelText, p1Ok, p2Ok;
	private Sprite[] roundsSheet;
	private bool isOnline;
	private int onlineState;
	private float lastTimeMessageReceived = 0.0f;
	public void Start() {
		StateControllerInit(false);
		GameObject g = GameObject.Find("Confetti") as GameObject;
		particles = g.GetComponent<ParticleSystem>();
		pars = new ParticleSystem.Particle[particles.maxParticles];

		int p1Wins = 0, p2Wins = 0;
		for(int i = 0; i < PD.playerOneWonRound.Count; i++) { if(PD.playerOneWonRound[i]) { p1Wins++; } else { p2Wins++; } }
		PersistData.C winChar = p1Wins>p2Wins?PD.p1Char:PD.p2Char;
		PersistData.C loseChar = p1Wins<p2Wins?PD.p1Char:PD.p2Char;
		GetGameObject(Vector3.zero, "BG", Resources.Load<Sprite>(SpritePaths.BGPath + PD.GetPlayerSpritePath(p1Wins>p2Wins?PD.p1Char:PD.p2Char, true)), false, "BG0");

		PD.sounds.SetMusicAndPlay(SoundPaths.M_Title_DerivPath + PD.GetPlayerSpritePath(winChar));

		winner = CreateActor(PD.GetPlayerSpritePath(winChar), new Vector3(-2.06f, -0.5f));
		winner.SetScale(0.4f).SetSprite(2, false).SetSortingLayer("BG1");

		PD.sounds.SetVoiceAndPlay(SoundPaths.NarratorPath + (Random.value > 0.5f ? "039" : "040"), 0);
		int narratorIndex = 24 + (int) winChar;
		PD.sounds.QueueVoice(SoundPaths.NarratorPath + narratorIndex.ToString("d3"));
		int val = Random.Range(70, 76);
		PD.sounds.QueueVoice(SoundPaths.VoicePath + PD.GetPlayerSpritePath(winChar) + "/" + val.ToString("d3"));
		PD.sounds.SetSoundVolume(PD.GetSaveData().savedOptions["vol_s"] / 115.0f);

		loser = CreateActor(PD.GetPlayerSpritePath(loseChar), new Vector3(2.81f, -1.25f), true);
		loser.SetSprite(loser.loseFrame, false).SetScale(0.2f).SetSortingLayer("BG1").SetTint(new Color(0.5f, 0.5f, 0.5f));
		GetGameObject(new Vector3(1.3f, 0.7f), "infoBox", Resources.Load<Sprite>(SpritePaths.DetailsBox));
		System.Xml.XmlNode top = GetXMLHead();
		FontData f = PD.mostCommonFont.Clone();
		f.scale = 0.07f;
		float x = 1.3f;
		GetMeshText(new Vector3(x, 1.5f), string.Format(GetXmlValue(top, "winstatement"), p1Wins>p2Wins?1:2), f);
		x = 1.2f; f.align = TextAlignment.Right; f.anchor = TextAnchor.MiddleRight; f.scale = 0.035f;
		GetMeshText(new Vector3(x, 0.9f), GetXmlValue(top, "wins") + ":", f);
		GetMeshText(new Vector3(x, 0.65f), GetXmlValue(top, "losses") + ":", f);
		GetMeshText(new Vector3(x, 0.4f), GetXmlValue(top, "totaltime") + ":", f);
		GetMeshText(new Vector3(x, 0.15f), GetXmlValue(top, "p1score") + ":", f);
		GetMeshText(new Vector3(x, -0.1f), GetXmlValue(top, "p2score") + ":", f);
		x = 1.7f; f.align = TextAlignment.Left; f.anchor = TextAnchor.MiddleLeft;
		GetMeshText(new Vector3(x, 0.9f), Mathf.Max(p1Wins, p2Wins).ToString(), f);
		GetMeshText(new Vector3(x, 0.65f), Mathf.Min(p1Wins, p2Wins).ToString(), f);
		GetMeshText(new Vector3(x, 0.4f), new ScoreTextFormatter().ConvertSecondsToMinuteSecondFormat(PD.totalRoundTime), f);
		GetMeshText(new Vector3(x, 0.15f), PD.totalP1RoundScore.ToString(), f);
		GetMeshText(new Vector3(x, -0.1f), PD.totalP2RoundScore.ToString(), f);
		applauseTimer = Random.Range(200, 220);
		if(PD.gameType == PersistData.GT.Online) {
			FontData f2 = PD.mostCommonFont.Clone(); f.scale = 0.03f;
			Sprite[] beginSheet = Resources.LoadAll<Sprite>(SpritePaths.LongButtons);
			begin = GetGameObject(new Vector3(0.0f, -1.31f), "Again", beginSheet[0], true, "HUD");
			beginText = GetMeshText(new Vector3(0.0f, -1.215f), string.Format(GetXmlValue(top, "again"), PD.GetP1InputName(InputMethod.KeyBinding.launch)), f2).gameObject;
			
			cancel = GetGameObject(new Vector3(0.0f, -1.7f), "Leave", beginSheet[0], true, "HUD");
			cancelText = GetMeshText(new Vector3(0.0f, -1.61f), string.Format(GetXmlValue(top, "leave"), PD.GetP1InputName(InputMethod.KeyBinding.back)), f2).gameObject;
			
			roundsSheet = Resources.LoadAll<Sprite>(SpritePaths.RoundStateIcons);
			
			p1Ok = GetGameObject(new Vector3(-0.25f, -1f), "P1 Ready", roundsSheet[2], true, "HUD");
			p2Ok = GetGameObject(new Vector3(0.25f, -1f), "P2 Ready", roundsSheet[2], true, "HUD");
			isOnline = true;
			onlineState = 0;
			theyreReady = false;

			Callback_statsReceived = Callback<UserStatsReceived_t>.Create(OnGetUserStats);

			SteamUserStats.RequestCurrentStats();
			SteamUserStats.RequestUserStats((CSteamID) PD.onlineOpponentId);
			didP1win = p1Wins > p2Wins;
			readyToEnd = false;
			wantToEnd = false;
		} else {
			isOnline = false;
		}
	}
	private int p1Score = -1, p2Score = -1;
	private bool didP1win, readyToEnd, wantToEnd;
	public void OnGetUserStats(UserStatsReceived_t pCallback) {
		if(pCallback.m_steamIDUser == SteamUser.GetSteamID()) {
			SteamUserStats.GetStat("GAMESCORE", out p1Score);
		} else if(pCallback.m_steamIDUser == (CSteamID) PD.onlineOpponentId) {
			SteamUserStats.GetUserStat((CSteamID) PD.onlineOpponentId, "GAMESCORE", out p2Score);
		}
		if(p1Score >= 0 && p2Score >= 0) {
			int newScore = p1Score;
			if(didP1win) {
				newScore += Mathf.FloorToInt((float)p2Score / 9f) + 1;
			} else {
				newScore -= Mathf.FloorToInt(Mathf.Max (0, p2Score - Mathf.Floor((float)p1Score / 2f)) / 5);
			}
			if(newScore < 0) { newScore = 0; }
			else if(newScore > 9999) { newScore = 9999; }
			Debug.Log ("Score went from " + p1Score + " to " + newScore);
			bool res = SteamUserStats.SetStat("GAMESCORE", newScore);
			res = SteamUserStats.StoreStats();
			readyToEnd = true;
		}
	}


	private bool theyreReady;
	private float timeToWait = 6.9f;
	private void UpdateOnline() {
		timeToWait -= Time.deltaTime;
		if(wantToEnd && (readyToEnd || timeToWait <= 0f)) {
			EndOnlineGame();
		}
		List<string> ms = GetMessages();
		if(ms.Count > 0) {
			foreach(string m in ms) {
				lastTimeMessageReceived = Time.time;
				if(m == "bye") {
					wantToEnd = true;
				} else if(m == "another") {
					p2Ok.GetComponent<SpriteRenderer>().sprite = roundsSheet[1];
					theyreReady = true;
				}
			}
		}
		if((Time.time - lastTimeMessageReceived) > 15f) {
			SendMessage("bye");
			wantToEnd = true;
		}
		if(onlineState == 0) { // awaiting player input
			if(PD.controller.M_Cancel()) {
				SendMessage("bye");
				wantToEnd = true;
			} else if(PD.controller.G_Launch() || PD.controller.Pause()) {
				SendMessage("another");
				onlineState = 1;
				p1Ok.GetComponent<SpriteRenderer>().sprite = roundsSheet[1];
				begin.SetActive(false);
				beginText.SetActive(false);
				cancel.SetActive(false);
				cancelText.SetActive(false);
			}
		} else if(onlineState == 1) { // awaiting opponent input
			if(theyreReady) { PD.MoveToOnlineBattle(); }
		}
	}
	private void EndOnlineGame() {
		onlineState = -1;
		PD.ChangeScreen(PersistData.GS.CharSel);
		SteamNetworking.CloseP2PSessionWithUser((CSteamID)PD.onlineOpponentId);
		PD.onlineOpponentId = 0;
	}
	private void SendMessage(string hello) {
		byte[] bytes = new byte[hello.Length * sizeof(char)];
		using (System.IO.MemoryStream ms = new System.IO.MemoryStream(bytes)) {
			using (System.IO.BinaryWriter b = new System.IO.BinaryWriter(ms)) {
				b.Write(hello);
			}
		}
		SteamNetworking.SendP2PPacket((CSteamID) PD.onlineOpponentId, bytes, (uint) bytes.Length, EP2PSend.k_EP2PSendReliable);
	}
	private List<string> GetMessages() {
		uint size;
		List<string> m = new List<string>();
		while (SteamNetworking.IsP2PPacketAvailable(out size)) {
			byte[] buffer = new byte[size];
			uint bytesRead;
			CSteamID remoteId;
			if (SteamNetworking.ReadP2PPacket(buffer, size, out bytesRead, out remoteId)) {
				using (System.IO.MemoryStream ms = new System.IO.MemoryStream(buffer)) {
					using (System.IO.BinaryReader b = new System.IO.BinaryReader(ms)) {
						string message = b.ReadString();
						m.Add(message);
					}
				}
			}
		}
		return m;
	}


	public void Update() {
		if(--applauseTimer == 0) { PD.sounds.SetSoundAndPlay(SoundPaths.S_Applause + Random.Range(1, 7).ToString()); }
		if(isOnline) {
			UpdateOnline();
			return;
		}
		if(PD.controller.G_Launch() || PD.controller.Pause() || clicker.isDown()) {
			PD.sounds.SetSoundVolume(PD.GetSaveData().savedOptions["vol_s"] / 150.0f);
			if(PD.gameType == PersistData.GT.Versus) {
				PD.ChangeScreen(PersistData.GS.CharSel);
			} else {
				PD.ChangeScreen(PersistData.GS.HighScore);
			}
		}
		int pCount = particles.GetParticles(pars);
		for(int i = 0; i < pCount; i++) {
			if(Mathf.Abs(pars[i].startLifetime - pars[i].lifetime) < 0.05f) {
				pars[i].color = new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), 1.0f);
				pars[i].size *= Random.Range(0.8f, 1.2f);
			}
		}
		particles.SetParticles(pars, pCount);
	}
}