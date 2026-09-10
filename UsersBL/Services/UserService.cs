using Shared.Common.Results;
using UsersBL.DTOs;
using UsersBL.Interfaces;
using UsersDA.Interfaces;

namespace UsersBL.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result<UserProfileResponse>.Failure("المستخدم غير موجود");

        return Result<UserProfileResponse>.Success(ToResponse(user));
    }

    public async Task<Result<UserProfileResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result<UserProfileResponse>.Failure("المستخدم غير موجود");

        user.FullName = request.FullName;
        user.Age = request.Age;

        await _unitOfWork.SaveChangesAsync(ct);

        return Result<UserProfileResponse>.Success(ToResponse(user));
    }

    private static UserProfileResponse ToResponse(UsersDA.Entities.User user) => new(
        user.Id, user.Email, user.FullName, user.Role, user.AuthProvider,
        user.Age, user.IsActive, user.ConvertedFromGuestAt, user.CreatedAt);
}
