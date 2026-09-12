using ContentBL.Interfaces;
using ContentBL.Services;
using ContentDA;
using ContentDA.Context;
using ContentDA.Interfaces;
using ContentDA.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Content;

namespace ContentBL;

public static class ContentModule
{
    public static IServiceCollection AddContentModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ContentDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IContentTypeRepository, ContentTypeRepository>();
        services.AddScoped<ILevelRepository, LevelRepository>();
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<ILessonContentRepository, LessonContentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IContentTypeService, ContentTypeService>();
        services.AddScoped<ILevelService, LevelService>();
        services.AddScoped<ILessonService, LessonService>();
        services.AddScoped<ILessonContentService, LessonContentService>();
        services.AddScoped<IImageStorageService, LocalImageStorageService>();

        // [Assessment-LessonQuiz] [Assessment-Placement] [Assessment-AI]
        // Registrations only, for the three Shared contracts Assessment consumes
        // (lesson published? level order? image bytes?). Nothing else in this
        // module changed. If these are ever removed, re-check the Assessment
        // quiz-for-lesson, placement and AI-image paths.
        //
        // عقود مشتركة (في Shared) بيستخدمها مودل الـ Assessment
        services.AddScoped<ILessonAvailability, LessonAvailabilityService>();
        services.AddScoped<ILevelCatalog, LevelCatalogService>();
        services.AddScoped<IMediaContentReader, LocalMediaContentReader>();

        return services;
    }
}
