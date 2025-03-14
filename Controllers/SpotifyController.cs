using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SpotifyAPI.Web;

namespace djbeb;

[Route("api/spotify")]
[ApiController]
public class SpotifyController : ControllerBase
{
    private readonly SpotifyService _spotifyService;
    private readonly SpotifyConfig _spotifyConfig;
    private readonly JwtConfig _jwtConfig;

    public SpotifyController(SpotifyService spotifyService, IOptionsSnapshot<SpotifyConfig> spotifyConfig, IOptionsSnapshot<JwtConfig> jwtConfig)
    {
        _spotifyService = spotifyService;
        _spotifyConfig = spotifyConfig.Value;
        _jwtConfig = jwtConfig.Value;
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

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtConfig.Secret!);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("SpotifyToken", response.AccessToken)
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var jwt = tokenHandler.WriteToken(token);

        var frontendUrl = _spotifyConfig.FrontendUrl;
        return Redirect($"{frontendUrl}?token={jwt}");
    }
    
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken()
    {
        var jwt = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(jwt)) return Unauthorized("No JWT provided");

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        var refreshToken = token.Claims.FirstOrDefault(c => c.Type == "SpotifyRefreshToken")?.Value;

        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized("Refresh token missing");

        var newToken = await _spotifyService.RefreshAccessToken(refreshToken);
        if (newToken == null)
            return Unauthorized("Failed to refresh access token");

        var jwtSecret = _jwtConfig.Secret;
        var key = Encoding.UTF8.GetBytes(jwtSecret!);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("SpotifyToken", newToken.AccessToken),
                new Claim("SpotifyRefreshToken", newToken.RefreshToken)
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var newJwt = handler.WriteToken(handler.CreateToken(tokenDescriptor));

        return Ok(new { jwtToken = newJwt });
    }

    private string? GetSpotifyTokenFromJwt()
    {
        var jwt = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(jwt)) return null;

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        return token.Claims.FirstOrDefault(c => c.Type == "SpotifyToken")?.Value;
    }
    

    [Authorize]
    [HttpGet("playlists")]
    public async Task<IActionResult> GetPlaylists()
    {
        var token = User.FindFirst("SpotifyToken")?.Value;
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ JWT is missing SpotifyToken claim.");

        var spotifyClient = new SpotifyClient(token);
        var playlists = await _spotifyService.GetUserPlaylists(spotifyClient);

        return Ok(playlists);
    }

    [Authorize]
    [HttpGet("playlist/{playlistId}")]
    public async Task<IActionResult> GetPlaylistTracks(string playlistId)
    {
        var token = User.FindFirst("SpotifyToken")?.Value;
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ JWT is missing SpotifyToken claim.");

        var tracksJson = await _spotifyService.GetPlaylistTracks(playlistId, token);
        return Content(tracksJson, "application/json");
    }

    [Authorize]
    [HttpPut("play")]
    public async Task<IActionResult> PlayTrack([FromBody] PlayTrackRequest request)
    {
        if (string.IsNullOrEmpty(request.DeviceId) || string.IsNullOrEmpty(request.TrackId) || string.IsNullOrEmpty(request.PlaylistId))
            return BadRequest("❌ Missing trackId, deviceId, or playlistId");

        var token = User.FindFirst("SpotifyToken")?.Value;
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ JWT is missing SpotifyToken claim.");

        var spotifyClient = new SpotifyClient(token);
        await _spotifyService.PlayTrack(spotifyClient, request.TrackId, request.DeviceId, request.PlaylistId);

        return Ok(new { message = "Track playing from playlist." });
    }

    [Authorize]
    [HttpPost("pause")]
    public async Task<IActionResult> PausePlayback()
    {
        var token = User.FindFirst("SpotifyToken")?.Value;
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ JWT is missing SpotifyToken claim.");

        var spotifyClient = new SpotifyClient(token);
        await _spotifyService.PausePlayback(spotifyClient);

        return Ok("✅ Playback paused.");
    }

    [Authorize]
    [HttpPost("resume")]
    public async Task<IActionResult> ResumePlayback([FromQuery] string device_id)
    {
        var token = User.FindFirst("SpotifyToken")?.Value;
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ JWT is missing SpotifyToken claim.");

        var spotifyClient = new SpotifyClient(token);
        await spotifyClient.Player.ResumePlayback(new PlayerResumePlaybackRequest
        {
            DeviceId = device_id
        });

        return Ok("✅ Playback resumed.");
    }

    [Authorize]
    [HttpPut("seek")]
    public async Task<IActionResult> SeekPlayback([FromBody] SeekRequest request)
    {
        var token = User.FindFirst("SpotifyToken")?.Value;
        if (string.IsNullOrEmpty(token))
            return Unauthorized("❌ JWT is missing SpotifyToken claim.");

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