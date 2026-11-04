namespace GuessWord.Contracts.Responses;

/// <summary>Участник текущего лобби.</summary>
public sealed record RoomMemberDto(int UserId, string Name, bool IsHost);
