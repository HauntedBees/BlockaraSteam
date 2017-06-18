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
using Steamworks;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;
public class OnlinePlayer:AICore {
	private PersistData PD;
	private CSteamID opponent;
	private List<AIAction> actionQueue;
	public OnlinePlayer(BoardWar mine, BoardWar theirs, BoardCursorActualCore cursor):base(mine, theirs, cursor) {
		state = 0;
		GameObject Persist = GameObject.Find("PersistData") as GameObject;
		PD = Persist.GetComponent<PersistData>();
		opponent = (CSteamID) PD.onlineOpponentId;
		actionQueue = new List<AIAction>();
	}
	public void PushAction(string m) {
		XmlSerializer xs = new XmlSerializer(typeof(AIAction));
		using(System.IO.TextReader tr = new System.IO.StringReader(m)) {
			AIAction a = (AIAction) xs.Deserialize(tr);
			actionQueue.Add(a);
		}
	}
	override public AIAction TakeAction() {
		if(actionQueue.Count == 0) { return null; }
		AIAction aia = actionQueue[0];
		actionQueue.RemoveAt(0);
		return aia;
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
						Debug.Log("Received a message: " + message);
					}
				}
			}
		}
		return m;
	}
}