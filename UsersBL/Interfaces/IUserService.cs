using Shared.Common.Results;
using UsersBL.DTOs;

namespace UsersBL.Interfaces;

public interface IUserService
{
    Task<Result<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserProfileResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);
}
