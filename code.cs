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

    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    public bool Execute()
    {
        _httpClient.DefaultRequestHeaders.Clear();

        Int16 daysOld = 60;
        bool isRandom = true;
        string shoutoutHtml = "https://justeDave.tv/shoutout/v2";
        string targetUser;
        string targetUserId;
        string targetUserProfileImageUrl;
        string shoutoutScene;
        string shoutoutSource;

        try
        {
            shoutoutScene = args["shoutoutScene"].ToString();
            shoutoutSource = args["shoutoutSource"].ToString();
            targetUser = args["targetUser"].ToString();
            targetUserId = args["targetUserId"].ToString();
            targetUserProfileImageUrl = args["targetUserProfileImageUrl"].ToString();
        }
        catch
        {
            CPH.LogError($"JDhoutout : Missing critical informations. Unable to continue.");
            throw;
        }

        try
        {
            daysOld = Int16.Parse(args["daysOld"].ToString());
        }
        catch
        {
            CPH.LogWarn($"JDhoutout : 'daysOld' is not defined or is not a number. Default value of {daysOld} applied.");
        }

        try
        {
            isRandom = Boolean.Parse(args["isRandom"].ToString());
        }
        catch
        {
            CPH.LogWarn($"JDhoutout : 'isRandom' is not defined or is not a Boolean value. Default value of {isRandom} applied");
        }

        try
        {
            shoutoutHtml = args["shoutoutHtml"].ToString();
        }
        catch
        {
            CPH.LogWarn($"JDhoutout : 'shoutoutHtml' is not defined. Default value of {shoutoutHtml} applied");
        }

        var startdate = DateTime.Now.AddDays(-daysOld);
        var allClips = CPH.GetClipsForUser(targetUser, startdate, DateTime.Now);

        if (allClips.Count == 0)
        {
            allClips = CPH.GetClipsForUser(targetUser);
        }

        if (allClips.Count == 0)
        {
            CPH.SendMessage("JDhoutout : No available clips are found for this streamer :(");
            return false;
        }

        ClipData selectedClip = null;
        if (isRandom)
        {
            var randomNumber = new Random();
            var clipid = randomNumber.Next(0, allClips.Count - 1);
            selectedClip = allClips[clipid];
        }
        else
        {
            allClips.Sort((clip1, clip2) => clip2.ViewCount.CompareTo(clip1.ViewCount));
            selectedClip = allClips[0];
        }

        int clipDuration;
        try
        {
            var clipDurationDouble = selectedClip.Duration * 1000;
            clipDuration = Convert.ToInt32(clipDurationDouble);
        }
        catch
        {
            CPH.LogError($"JDhoutout : Cannot parse {selectedClip.Duration} to Int...");
            throw;
        }

        var clipTitle = selectedClip.Title;
        shoutoutHtml += $"?targetUser={targetUser}&targetUserProfileImageUrl={targetUserProfileImageUrl}&clipDuration={clipDuration}&id={selectedClip.Id}&clipTitle={clipTitle}";
        CPH.SetArgument("shoutoutHtml", shoutoutHtml);

        int shoutoutTotalDuration = 3000 + clipDuration + 3000; // 3 seconds for the profileImage appearance + duration of the clip +  3 seconds for the appearance and fade out
        CPH.SetArgument("shoutoutTotalDuration", shoutoutTotalDuration);

        CPH.ObsSetBrowserSource(shoutoutScene, shoutoutSource, shoutoutHtml);
        CPH.ObsSetSourceVisibility(shoutoutScene, shoutoutSource, true);
        CPH.Wait(shoutoutTotalDuration);
        CPH.ObsSetSourceVisibility(shoutoutScene, shoutoutSource, false);
        CPH.ObsSetBrowserSource(shoutoutScene, shoutoutSource, "about:blank");

        return true;
    }
}