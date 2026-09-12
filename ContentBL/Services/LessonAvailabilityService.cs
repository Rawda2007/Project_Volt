using ContentDA.Interfaces;
using Shared.Content;

namespace ContentBL.Services;

// [Assessment-LessonQuiz] Implements the Shared contract ILessonAvailability so
// Assessment can gate GET /api/quizzes/for-lesson and StartAsync on a published
// lesson without referencing ContentBL. Read-only; no Content table or entity was
// added or changed. Do not extend beyond this lookup.
//
// بيجاوب المودلات التانية (زي الـ Assessment) على سؤال واحد بس: الدرس موجود؟ ومنشور؟
// من غير ما يعملوا Reference لمودل الـ Content كله.
public class LessonAvailabilityService : ILessonAvailability
{
    private readonly ILessonRepository _lessonRepository;

    public LessonAvailabilityService(ILessonRepository lessonRepository) => _lessonRepository = lessonRepository;

    public async Task<bool?> IsPublishedAsync(int lessonId, CancellationToken cancellationToken = default)
    {
        var lesson = await _lessonRepository.GetByIdAsync(lessonId, cancellationToken);
        return lesson?.IsPublished;
    }
}
