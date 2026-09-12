using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UsersBL.Interfaces;
using UsersBL.Services;
using UsersDA;
using UsersDA.Context;
using UsersDA.Interfaces;
using UsersDA.Repositories;

namespace UsersBL;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<UsersDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetOtpRepository, PasswordResetOtpRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        // [Assessment-AI] Registration only, for the Shared contract Assessment
        // consumes to read a learner's age for AI hints. Nothing else in Users
        // changed. See LearnerProfileService.
        services.AddScoped<Shared.Users.ILearnerProfile, LearnerProfileService>();

        return services;
    }
}
