using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bloxstrap.Utility
{
    public class FriendEntry
    {
        public long Id { get; set; }
        public string DisplayName { get; set; } = "";
        public int PresenceType { get; set; }
        public long? PlaceId { get; set; }
    }

    public class PresenceRequest
    {
        [JsonPropertyName("userIds")]
        public List<long> UserIds { get; set; } = new();
    }

    public class PresenceResponse
    {
        [JsonPropertyName("userPresences")]
        public List<PresenceEntry> UserPresences { get; set; } = new();
    }

    public class PresenceEntry
    {
        [JsonPropertyName("userId")]
        public long UserId { get; set; }

        [JsonPropertyName("userPresenceType")]
        public int UserPresenceType { get; set; }

        [JsonPropertyName("placeId")]
        public long? PlaceId { get; set; }
    }

    public static class FriendsService
    {
        public static async Task<List<FriendEntry>> GetOnlineFriends()
        {
            var friends = await Http.AuthGetJson<Dictionary<string, JsonElement>>(
                new Uri("https://friends.roblox.com/v1/my/friends/online"));

            var result = new List<FriendEntry>();

            if (!friends.TryGetValue("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var friend in dataElement.EnumerateArray())
            {
                long id = friend.GetProperty("id").GetInt64();
                string name = friend.GetProperty("displayName").GetString() ?? "";

                result.Add(new FriendEntry { Id = id, DisplayName = name });
            }

            if (!result.Any())
                return result;

            var request = new HttpRequestMessage(HttpMethod.Post, "https://presence.roblox.com/v1/presence/users");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new PresenceRequest { UserIds = result.Select(x => x.Id).ToList() }),
                Encoding.UTF8,
                "application/json");

            var presence = await Http.AuthSendJson<Bloxstrap.Models.APIs.Roblox.PresenceResponse>(request);

            foreach (var entry in result)
            {
                var match = presence.UserPresences.FirstOrDefault(x => x.UserId == entry.Id);

                if (match is not null)
                {
                    entry.PresenceType = match.UserPresenceType;
                    entry.PlaceId = match.PlaceId;
                }
            }

            return result;
        }
    }
}
