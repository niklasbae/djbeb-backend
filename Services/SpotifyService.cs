using System.Net.Http.Headers;
using djbeb;
using SpotifyAPI.Web;
using Microsoft.Extensions.Options;

namespace djbeb;

public class SpotifyService
{
    private readonly SpotifyConfig _config;

    public SpotifyService(IOptions<SpotifyConfig> config)
    {
        _config = config.Value;
    }

    public string GetLoginUrl()
    {
        var loginRequest = new LoginRequest(
            new Uri(_config.RedirectUri),
            _config.ClientId,
            LoginRequest.ResponseType.Code
        )
        {
            Scope = new[] {
                Scopes.PlaylistReadPrivate,
                Scopes.UserReadPlaybackState,
                Scopes.UserModifyPlaybackState,
                Scopes.UserReadCurrentlyPlaying,
                Scopes.Streaming,
                Scopes.AppRemoteControl,
                Scopes.UserReadEmail,
                Scopes.UserReadPrivate
                
            }
        };
        return loginRequest.ToUri().ToString();
    }
    
    public async Task<AuthorizationCodeTokenResponse> RequestToken(string code)
    {
        return await new OAuthClient().RequestToken(
            new AuthorizationCodeTokenRequest(
                _config.ClientId, _config.ClientSecret, code, new Uri(_config.RedirectUri)
            )
        );
    }

    public async Task<SpotifyClient> GetClientByCode(string code)
    {
        var response = await new OAuthClient().RequestToken(
            new AuthorizationCodeTokenRequest(
                _config.ClientId, _config.ClientSecret, code, new Uri(_config.RedirectUri)
            )
        );

        return new SpotifyClient(response.AccessToken);
    }

    public async Task<Paging<FullPlaylist>> GetUserPlaylists(SpotifyClient client)
    {
        return await client.Playlists.CurrentUsers();
    }

    public async Task<string> GetPlaylistTracks(string playlistId, string accessToken)
    {
        using var httpClient = new HttpClient();

        string requestUrl = $"https://api.spotify.com/v1/playlists/{playlistId}/tracks";

        try
        {
            // ✅ Set Authorization Header
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // ✅ Make GET Request
            var response = await httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();

            // ✅ Read and Log Response
            var jsonResponse = await response.Content.ReadAsStringAsync();

            return jsonResponse;
        }
        catch (Exception ex)
        {
            // ✅ Log and Handle Errors
            Console.WriteLine($"❌ Error fetching playlist tracks: {ex.Message}");
            throw;
        }
    }

    public async Task PlayTrack(SpotifyClient client, string trackId, string deviceId, string playlistId)
    {
        var request = new PlayerResumePlaybackRequest
        {
            ContextUri = $"spotify:playlist:{playlistId}",  // ✅ Play the entire playlist
            OffsetParam = new PlayerResumePlaybackRequest.Offset
            {
                Uri = $"spotify:track:{trackId}"  // ✅ Start at the selected track
            },
            DeviceId = deviceId
        };

        await client.Player.ResumePlayback(request);
    }

    public async Task PausePlayback(SpotifyClient client)
    {
        await client.Player.PausePlayback();
    }
    
    
}