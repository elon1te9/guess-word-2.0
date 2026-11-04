using GuessWord.Client.Infrastructure.Http;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;

namespace GuessWord.Client.Infrastructure.Http;

public class RoomService
{
    private readonly ApiRequestService _apiRequestService;

    public RoomService(ApiRequestService apiRequestService)
    {
        _apiRequestService = apiRequestService;
    }

    public Task<RoomResponseDto?> GetCurrentRoomAsync(CancellationToken cancellationToken = default)
        => _apiRequestService.GetAsync<RoomResponseDto>("api/room/current", cancellationToken);

    public async Task<RoomResponseDto?> CreateRoomAsync()
    {
        return await _apiRequestService.PostAsync<RoomResponseDto>("api/room/create");
    }

    public async Task<RoomResponseDto?> JoinRoomAsync(string code)
    {
        var request = new JoinRoomRequestDto
        {
            Code = code
        };

        return await _apiRequestService.PostAsync<JoinRoomRequestDto, RoomResponseDto>("api/room/join", request);
    }

    public async Task<RoomResponseDto?> GetRoomAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _apiRequestService.GetAsync<RoomResponseDto>($"api/room/{code}", cancellationToken);
    }

    public async Task<bool> LeaveRoomAsync(string code)
    {
        return await _apiRequestService.PostAsync($"api/room/{code}/leave");
    }
}
