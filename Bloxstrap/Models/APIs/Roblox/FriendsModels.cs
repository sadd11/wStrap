using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bloxstrap.Models.APIs.Roblox
{
    public class FriendsOnlineResponse
    {
        [JsonPropertyName("data")]
        public List<FriendData> Data { get; set; } = new();
    }

    public class FriendData
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";
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
}
