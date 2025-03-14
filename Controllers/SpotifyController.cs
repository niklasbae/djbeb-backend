using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace djbeb;

[Route("api/spotify")]
[ApiController]
public class SpotifyController : ControllerBase
{
    private readonly SpotifyService _spotifyService;
    private readonly SpotifyConfig _spotifyConfig;
    
    public SpotifyController(SpotifyService spotifyService, IOptionsSnapshot<SpotifyConfig> spotifyConfig)
    {
        _spotifyService = spotifyService;
        _spotifyConfig = spotifyConfig.Value;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        var url = _spotifyService.GetLoginUrl();
        return Redirect(url);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code)
    {
        var response = await _spotifyService.RequestToken(code);
        HttpContext.Session.SetString("SpotifyToken", response.AccessToken);

        // ✅ Dynamically fetch frontend URL from appsettings
        var frontendUrl = _spotifyConfig.FrontendUrl ?? "http://localhost:5173";
        return Redirect(frontendUrl);
    }

    [HttpGet("playlists")]
    public async Task<IActionResult> GetPlaylists()
    {
        var token = HttpContext.Session.GetString("SpotifyToken");
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ You must authenticate first.");

        var spotifyClient = new SpotifyClient(token);
        var playlists = await _spotifyService.GetUserPlaylists(spotifyClient);

        return Ok(playlists);
    }

    [HttpGet("playlist/{playlistId}")]
    public async Task<IActionResult> GetPlaylistTracks(string playlistId)
    {
        var token = HttpContext.Session.GetString("SpotifyToken");
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ Missing access token.");

        var tracksJson = await _spotifyService.GetPlaylistTracks(playlistId, token);
        return Content(tracksJson, "application/json");
    }

    [HttpPut("play")]
    public async Task<IActionResult> PlayTrack([FromBody] PlayTrackRequest request)
    {
        if (string.IsNullOrEmpty(request.DeviceId) || string.IsNullOrEmpty(request.TrackId) || string.IsNullOrEmpty(request.PlaylistId))
            return BadRequest("❌ Missing trackId, deviceId, or playlistId");

        var token = HttpContext.Session.GetString("SpotifyToken");
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ You must authenticate first.");

        var spotifyClient = new SpotifyClient(token);
        await _spotifyService.PlayTrack(spotifyClient, request.TrackId, request.DeviceId, request.PlaylistId);

        return Ok(new { message = "Track playing from playlist." });    
    }

    [HttpPost("pause")]
    public async Task<IActionResult> PausePlayback()
    {
        var token = HttpContext.Session.GetString("SpotifyToken");
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ You must authenticate first.");

        var spotifyClient = new SpotifyClient(token);
        await _spotifyService.PausePlayback(spotifyClient);

        return Ok("✅ Playback paused.");
    }

    [HttpGet("token")]
    public IActionResult GetSpotifyToken()
    {
        var token = HttpContext.Session.GetString("SpotifyToken");
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ You must authenticate first.");

        return Ok(new { access_token = token });
    }

    [HttpPost("resume")]
    public async Task<IActionResult> ResumePlayback([FromQuery] string device_id)
    {
        var token = HttpContext.Session.GetString("SpotifyToken");
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ You must authenticate first.");

        var spotifyClient = new SpotifyClient(token);
        await spotifyClient.Player.ResumePlayback(new PlayerResumePlaybackRequest
        {
            DeviceId = device_id
        });

        return Ok("✅ Playback resumed.");
    }

    [HttpPut("seek")]
    public async Task<IActionResult> SeekPlayback([FromBody] SeekRequest request)
    {
        var token = HttpContext.Session.GetString("SpotifyToken");
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ You must authenticate first.");

        var spotifyClient = new SpotifyClient(token);
        await spotifyClient.Player.SeekTo(new PlayerSeekToRequest(request.PositionMs)
        {
            DeviceId = request.DeviceId
        });

        return Ok(new { message = "✅ Playback position updated." });
    }

    public class PlayTrackRequest
    {
        public string TrackId { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public string PlaylistId { get; set; } = ""; 
    }

    public class SeekRequest
    {
        public int PositionMs { get; set; }
        public string DeviceId { get; set; } = "";
    }
}