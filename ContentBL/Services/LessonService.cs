using ContentBL.DTOs;
using ContentBL.Interfaces;
using ContentDA.Entities;
using ContentDA.Interfaces;
using Shared.Common.Results;

namespace ContentBL.Services;

public class LessonService : ILessonService
{
    private readonly ILessonRepository _lessonRepository;
    private readonly ILessonContentRepository _lessonContentRepository;
    private readonly ILevelRepository _levelRepository;
    private readonly IImageStorageService _imageStorageService;
    private readonly IUnitOfWork _unitOfWork;

    public LessonService(
        ILessonRepository lessonRepository,
        ILessonContentRepository lessonContentRepository,
        ILevelRepository levelRepository,
        IImageStorageService imageStorageService,
        IUnitOfWork unitOfWork)
    {
        _lessonRepository = lessonRepository;
        _lessonContentRepository = lessonContentRepository;
        _levelRepository = levelRepository;
        _imageStorageService = imageStorageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<LessonSummaryResponse>>> GetByLevelIdAsync(int levelId, CancellationToken ct = default)
    {
        var lessons = await _lessonRepository.GetByLevelIdAsync(levelId, ct);
        return Result<List<LessonSummaryResponse>>.Success(lessons.Select(ToSummary).ToList());
    }

    public async Task<Result<LessonDetailResponse>> GetDetailAsync(int id, CancellationToken ct = default)
    {
        var lesson = await _lessonRepository.GetWithContentsAsync(id, ct);
        if (lesson is null)
            return Result<LessonDetailResponse>.Failure("الدرس غير موجود");

        var contents = lesson.LessonContents
            .Select(c => new LessonContentResponse(c.Id, c.LessonId, c.ContentTypeId, c.ContentType.Name, c.Content, c.MediaUrl, c.SortOrder))
            .ToList();

        var response = new LessonDetailResponse(
            lesson.Id, lesson.LevelId, lesson.Title, lesson.Description,
            lesson.SortOrder, lesson.IsPublished, lesson.CreatedAt, contents);

        return Result<LessonDetailResponse>.Success(response);
    }

    public async Task<Result<LessonSummaryResponse>> CreateAsync(CreateLessonRequest request, CancellationToken ct = default)
    {
        if (await _levelRepository.GetByIdAsync(request.LevelId, ct) is null)
            return Result<LessonSummaryResponse>.Failure("المستوى المحدد غير موجود");

        var nextOrder = await _lessonRepository.GetMaxSortOrderAsync(request.LevelId, ct) + 1;

        var lesson = new Lesson
        {
            LevelId = request.LevelId,
            Title = request.Title,
            Description = request.Description,
            SortOrder = nextOrder,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow
        };

        await _lessonRepository.AddAsync(lesson, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<LessonSummaryResponse>.Success(ToSummary(lesson));
    }

    public async Task<Result<LessonSummaryResponse>> UpdateAsync(int id, UpdateLessonRequest request, CancellationToken ct = default)
    {
        var lesson = await _lessonRepository.GetByIdAsync(id, ct);
        if (lesson is null)
            return Result<LessonSummaryResponse>.Failure("الدرس غير موجود");

        if (await _levelRepository.GetByIdAsync(request.LevelId, ct) is null)
            return Result<LessonSummaryResponse>.Failure("المستوى المحدد غير موجود");

        lesson.Title = request.Title;
        lesson.Description = request.Description;

        // لو الدرس بيتنقل لمستوى تاني، مالوش معنى يفضل مقاسم في نفس رقم الترتيب القديم
        // (ممكن يتكرر مع درس تاني في المستوى الجديد) - فبنديله ترتيب جديد في نهاية المستوى الجديد
        if (lesson.LevelId != request.LevelId)
        {
            lesson.SortOrder = await _lessonRepository.GetMaxSortOrderAsync(request.LevelId, ct) + 1;
            lesson.LevelId = request.LevelId;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<LessonSummaryResponse>.Success(ToSummary(lesson));
    }

    public async Task<Result<LessonSummaryResponse>> SetPublishedAsync(int id, bool isPublished, CancellationToken ct = default)
    {
        var lesson = await _lessonRepository.GetByIdAsync(id, ct);
        if (lesson is null)
            return Result<LessonSummaryResponse>.Failure("الدرس غير موجود");

        lesson.IsPublished = isPublished;
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<LessonSummaryResponse>.Success(ToSummary(lesson));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var lesson = await _lessonRepository.GetByIdAsync(id, ct);
        if (lesson is null)
            return Result.Failure("الدرس غير موجود");

        var contents = await _lessonContentRepository.GetByLessonIdAsync(id, ct);
        foreach (var content in contents)
            _imageStorageService.DeleteImage(content.MediaUrl);

        _lessonContentRepository.RemoveRange(contents);
        _lessonRepository.Remove(lesson);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SwapOrderAsync(SwapLessonsOrderRequest request, CancellationToken ct = default)
    {
        if (request.FirstLessonId == request.SecondLessonId)
            return Result.Failure("مينفعش تبدلي ترتيب الدرس بنفسه");

        var first = await _lessonRepository.GetByIdAsync(request.FirstLessonId, ct);
        var second = await _lessonRepository.GetByIdAsync(request.SecondLessonId, ct);

        if (first is null || second is null)
            return Result.Failure("واحد من الدرسين غير موجود");

        if (first.LevelId != second.LevelId)
            return Result.Failure("مينفعش تبدلي ترتيب درسين من مستويين مختلفين");

        (first.SortOrder, second.SortOrder) = (second.SortOrder, first.SortOrder);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static LessonSummaryResponse ToSummary(Lesson lesson) => new(
        lesson.Id, lesson.LevelId, lesson.Title, lesson.Description,
        lesson.SortOrder, lesson.IsPublished, lesson.CreatedAt);
}
