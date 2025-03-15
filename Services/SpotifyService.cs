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
    
    public async Task<AuthorizationCodeRefreshResponse> RefreshAccessToken(string refreshToken)
    {
        try
        {
            var tokenRequest = new AuthorizationCodeRefreshRequest(
                _config.ClientId, _config.ClientSecret, refreshToken
            );

            var response = await new OAuthClient().RequestToken(tokenRequest);
            return response; // ✅ Correct return type
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error refreshing access token: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GetPlaylistTracks(string playlistId, string accessToken)
    {
        using var httpClient = new HttpClient();

        string requestUrl = $"https://api.spotify.com/v1/playlists/{playlistId}/tracks";

        try
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error fetching playlist tracks: {ex.Message}");
            throw;
        }
    }

    public async Task PlayTrack(SpotifyClient client, string trackId, string deviceId, string playlistId)
    {
        var request = new PlayerResumePlaybackRequest
        {
            ContextUri = $"spotify:playlist:{playlistId}",
            OffsetParam = new PlayerResumePlaybackRequest.Offset { Uri = $"spotify:track:{trackId}" },
            DeviceId = deviceId
        };

        await client.Player.ResumePlayback(request);
    }

    public async Task PausePlayback(SpotifyClient client)
    {
        await client.Player.PausePlayback();
    }
    
    public async Task<Paging<FullPlaylist>> GetUserPlaylists(SpotifyClient client)
    {
        try
        {
            var playlists = await client.Playlists.CurrentUsers();
            return playlists;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error fetching user playlists: {ex.Message}");
            throw;
        }
    }
}