namespace ContentBL.DTOs;

public record LessonSummaryResponse(
    int Id, int LevelId, string Title, string? Description,
    int SortOrder, bool IsPublished, DateTime CreatedAt);

public record LessonDetailResponse(
    int Id, int LevelId, string Title, string? Description,
    int SortOrder, bool IsPublished, DateTime CreatedAt,
    List<LessonContentResponse> Contents);

// مفيش SortOrder هنا - السيرفر بيحدده تلقائي (آخر ترتيب في نفس المستوى + 1)
public record CreateLessonRequest(int LevelId, string Title, string? Description);

// مفيش SortOrder هنا برضو - لو LevelId اتغيّر (نقل الدرس لمستوى تاني)، السيرفر
// هيدّيله ترتيب جديد تلقائي في نهاية المستوى الجديد. غير كده الترتيب مايتلمسش.
public record UpdateLessonRequest(int LevelId, string Title, string? Description);

public record SetPublishedRequest(bool IsPublished);

public record SwapLessonsOrderRequest(int FirstLessonId, int SecondLessonId);
