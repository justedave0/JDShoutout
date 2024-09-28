using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Twitch.Common.Models.Api;
using Newtonsoft.Json.Linq;
using System.Linq;

public class CPHInline
{	
    private string _accessToken => CPH.TwitchOAuthToken;
    private string _clientId => CPH.TwitchClientId;

    private static readonly HttpClient _httpClient = new HttpClient{Timeout = TimeSpan.FromSeconds(30)}; // Adjust the timeout as needed
    
    public bool Execute()
    {
    	_httpClient.DefaultRequestHeaders.Clear();
    	
    	Int16 dateRange = 60;
    	bool isRandom = true;	
    	string shoutoutHtml = "https://justeDave.tv/shoutout.html"; 	
    	string targetUser;
    	string targetUserId;
    	string targetUserProfileImageUrl;

    	try{
    		targetUser = args["targetUser"].ToString();
    		targetUserId = args["targetUserId"].ToString();
    		targetUserProfileImageUrl = args["targetUserProfileImageUrl"].ToString();
    	}
    	catch{
    		CPH.LogError($"Variables nécessaires introuvables. Impossible de continuer le shoutout.");
    		return false;
    	}
    	
    	try{
    		dateRange = Int16.Parse(args["dateRange"].ToString());
    	}
    	catch{
    		CPH.LogWarn($"Variable 'daterange' non définie ou n'est pas un nombre. Valeur par défaut utilisée ({dateRange})");
    	}
    	
    	try{
    		isRandom = Boolean.Parse(args["isRandom"].ToString());
    	}
    	catch{
    		CPH.LogWarn($"Variable 'isRandom' non définie ou n'est pas 'True' ou 'False'. Valeur par défaut utilisée ({isRandom})");
    	}
    	
    	try{
    		shoutoutHtml = args["shoutoutHtml"].ToString();
    	}
    	catch{
    		CPH.LogWarn($"Variable 'shoutoutHtml' non définie. Valeur par défaut utilisée ({shoutoutHtml})");
    	}
    	    	    	
    	var startdate = DateTime.Now.AddDays(-dateRange);
    	var allClips = CPH.GetClipsForUser(targetUser, startdate, DateTime.Now);
    	
    	// si aucun clips trouvés pour les "X" derniers jours, prendre les clips depuis toujours
    	if (allClips.Count == 0){
            allClips = CPH.GetClipsForUser(targetUser);
        }
        // si vraiement aucun clips trouvés, arrêter ici mais le signifier :(
        if (allClips.Count == 0){
            CPH.SendMessage("Je ne peux malheureusement pas trouver de clip pour ce streameur :(");
            return false;
        }

		ClipData selectedClip = null;
		if (isRandom){
			var randomNumber = new Random();
			var clipid = randomNumber.Next(0,allClips.Count-1);
			selectedClip = allClips[clipid];
		}
		else { 
			// si pas aléatoire, ordonner la liste du plus vu au moins vu et prendre le premier
			allClips.Sort((clip1, clip2) => clip2.ViewCount.CompareTo(clip1.ViewCount));
			selectedClip = allClips[0];
		}	
					
		var clipduration = selectedClip.Duration * 1000;
		var clipTitle = selectedClip.Title;
		shoutoutHtml += $"?targetUser={targetUser}&targetUserProfileImageUrl={targetUserProfileImageUrl}&clipduration={clipduration}&id={selectedClip.Id}&clipTitle={clipTitle}";
		CPH.SetArgument("shoutoutHtml", shoutoutHtml);
		
		// 3 secondes pour l'apparition au début, clipduration pour le clip lui-même et 3 secondes pour le cleanup de fin
		var shoutoutTotalDuration = 3000 + clipduration + 3000;
		CPH.SetArgument("shoutoutTotalDuration", shoutoutTotalDuration); 
				
        return true;
    }
    
    List<string> GetTopTrois(string targetUserId)
    {
        var url = $"https://api.twitch.tv/helix/videos?user_id={targetUserId}&type=archive";
        var gamesPlayed = new List<string>();

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Client-ID", _clientId);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");

            var response = client.GetAsync(url).Result;
            var jsonResponse = response.Content.ReadAsStringAsync().Result;
            var json = JObject.Parse(jsonResponse);
            CPH.LogDebug(jsonResponse);
            if (json["data"] != null && json["data"].HasValues)
            {
                foreach (var stream in json["data"])
                {
                    var gameName = stream["game_name"].ToString();
                    gamesPlayed.Add(gameName);
                }
            }
        }
		var topGames = gamesPlayed.GroupBy(game => game)
                        .OrderByDescending(g => g.Count())
                        .Take(3)
                        .Select(g => g.Key).ToList();   
        return topGames;
    }
    
}
