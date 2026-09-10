using AssessmentDA.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AssessmentBL.Interfaces;
using AssessmentBL.Services;

namespace AssessmentBL
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddAssessmentModule(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AssessmentDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            // مستقبلاً هنسجل هنا  الـ Services الخاصة بالـ Assessment
            services.AddScoped<IQuizAttemptService, QuizAttemptService>();
            services.AddScoped<IUserTopicStatService, UserTopicStatService>();

            return services;
        }
    }
}
