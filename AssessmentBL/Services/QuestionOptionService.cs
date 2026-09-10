using AssessmentBL.DTOs.QuestionOption;
using AssessmentBL.Interfaces;
using AssessmentDA.Context;
using AssessmentDA.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Shared.Common.Exceptions;

namespace AssessmentBL.Services
{
    // for admin use only; children do not create or update options, they only pick them
    public class QuestionOptionService : IQuestionOptionServiceForAdmin
    {
        private static readonly Expression<Func<QuestionOption, AdminQuestionOptionResponseDto>> ProjectToResponse =
            option => new AdminQuestionOptionResponseDto
            {
                Id = option.Id,
                OptionText = option.OptionText,
                IsCorrect = option.IsCorrect,
                DisplayOrder = option.DisplayOrder
            };

        private readonly AssessmentDbContext _db;

        public QuestionOptionService(AssessmentDbContext db) => _db = db;

        public async Task<IReadOnlyList<AdminQuestionOptionResponseDto>> GetByQuestionIdAsync(int questionId, CancellationToken cancellationToken = default)
        {
            var questionExists = await _db.Questions.AsNoTracking().AnyAsync(q => q.Id == questionId, cancellationToken: cancellationToken);
            if (!questionExists)
                throw new KeyNotFoundException($"السؤال رقم {questionId} غير موجود");

            return await _db.QuestionOptions
                .AsNoTracking()
                .Where(o => o.QuestionId == questionId)
                .OrderBy(o => o.DisplayOrder)
                .Select(ProjectToResponse)
                .ToListAsync(cancellationToken: cancellationToken);
        }

        public async Task<AdminQuestionOptionResponseDto> CreateOptionAsync(CreateQuestionOptionDto request, CancellationToken cancellationToken = default)
        {
            var questionExists = await _db.Questions.AsNoTracking().AnyAsync(q => q.Id == request.QuestionId, cancellationToken: cancellationToken);
            if (!questionExists)
                throw new KeyNotFoundException($"السؤال رقم {request.QuestionId} غير موجود");

            var optionText = NormalizeOptionText(request.OptionText);

            await EnsureDisplayOrderIsFreeAsync(request.QuestionId, request.DisplayOrder, excludingOptionId: null, cancellationToken: cancellationToken);

            if (request.IsCorrect)
                await EnsureNoOtherCorrectOptionAsync(request.QuestionId, excludingOptionId: null, cancellationToken: cancellationToken);

            var option = new QuestionOption
            {
                QuestionId = request.QuestionId,
                OptionText = optionText,
                IsCorrect = request.IsCorrect,
                DisplayOrder = request.DisplayOrder
                // CreatedAt is filled by the sysutcdatetime() column default.
            };

            _db.QuestionOptions.Add(option);
            await SaveWithOptionConflictAsync(request.QuestionId, request.DisplayOrder, cancellationToken);

            return ToResponse(option);
        }

        public async Task<AdminQuestionOptionResponseDto> UpdateOptionAsync(
     int optionId,
     UpdateQuestionOptionDto request, CancellationToken cancellationToken = default)
        {
            var option = await _db.QuestionOptions
                .FirstOrDefaultAsync(o => o.Id == optionId, cancellationToken: cancellationToken)
                ?? throw new KeyNotFoundException($"الاختيار رقم {optionId} غير موجود");

            var optionText = NormalizeOptionText(request.OptionText);

            if (option.DisplayOrder != request.DisplayOrder)
                await EnsureDisplayOrderIsFreeAsync(
                    option.QuestionId,
                    request.DisplayOrder,
                    excludingOptionId: optionId,
                    cancellationToken: cancellationToken);

            if (request.IsCorrect && !option.IsCorrect)
                await EnsureNoOtherCorrectOptionAsync(
                    option.QuestionId,
                    excludingOptionId: optionId,
                    cancellationToken: cancellationToken);

            var questionIsActive = await _db.Questions
                .AsNoTracking()
                .Where(q => q.Id == option.QuestionId)
                .Select(q => q.IsActive)
                .FirstAsync(cancellationToken: cancellationToken);

            if (questionIsActive && option.IsCorrect && !request.IsCorrect)
            {
                var correctOptionsCount = await _db.QuestionOptions
                    .AsNoTracking()
                    .CountAsync(o =>
                        o.QuestionId == option.QuestionId &&
                        o.IsCorrect,
                        cancellationToken: cancellationToken);

                if (correctOptionsCount == 1)
                    throw new InvalidOperationException(
                        $"لا يمكن إلغاء الإجابة الصحيحة الوحيدة من السؤال رقم {option.QuestionId} وهو مفعّل");
            }

            option.OptionText = optionText;
            option.IsCorrect = request.IsCorrect;
            option.DisplayOrder = request.DisplayOrder;

            await SaveWithOptionConflictAsync(option.QuestionId, request.DisplayOrder, cancellationToken);

            return ToResponse(option);
        }
        public async Task DeleteOptionAsync(int optionId, CancellationToken cancellationToken = default)
        {
            var option = await _db.QuestionOptions
                .FirstOrDefaultAsync(o => o.Id == optionId, cancellationToken: cancellationToken)
                ?? throw new KeyNotFoundException($"الاختيار رقم {optionId} غير موجود");

            var isReferenced = await _db.QuizAttemptMistakes
                .AsNoTracking()
                .AnyAsync(m => m.SelectedOptionId == optionId, cancellationToken: cancellationToken);

            if (isReferenced)
                throw new InvalidOperationException(
                    $"لا يمكن حذف الاختيار رقم {optionId} لأنه مستخدم في محاولات سابقة");

            // FK_QuizAttemptQuestions_QuestionId_CorrectOptionId would block this
            // anyway; checking here turns it into a clear business message.
            var isSnapshottedAnswerKey = await _db.QuizAttemptQuestions
                .AsNoTracking()
                .AnyAsync(aq => aq.CorrectOptionId == optionId, cancellationToken: cancellationToken);

            if (isSnapshottedAnswerKey)
                throw new InvalidOperationException(
                    $"لا يمكن حذف الاختيار رقم {optionId} لأنه الإجابة الصحيحة المسجّلة في محاولات سابقة");

            var questionIsActive = await _db.Questions
                .AsNoTracking()
                .Where(q => q.Id == option.QuestionId)
                .Select(q => q.IsActive)
                .FirstAsync(cancellationToken: cancellationToken);

            if (questionIsActive && option.IsCorrect)
            {
                var correctOptionsCount = await _db.QuestionOptions
                    .AsNoTracking()
                    .CountAsync(o =>
                        o.QuestionId == option.QuestionId &&
                        o.IsCorrect,
                        cancellationToken: cancellationToken);

                if (correctOptionsCount == 1)
                    throw new InvalidOperationException(
                        $"لا يمكن حذف الإجابة الصحيحة الوحيدة من السؤال رقم {option.QuestionId} وهو مفعّل");
            }

            _db.QuestionOptions.Remove(option);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // The EnsureXxx pre-checks below are friendly validation only; two admins
        // can pass them concurrently. UQ_QuestionOptions_QuestionId_DisplayOrder
        // and UQ_QuestionOptions_OneCorrectPerQuestion are the real protection,
        // and this turns losing either race into a 409 rather than a 500.
        private async Task SaveWithOptionConflictAsync(
            int questionId, short displayOrder, CancellationToken cancellationToken)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken: cancellationToken);
            }
            catch (DbUpdateException ex) when (
                ex.IsUniqueViolationOf("UQ_QuestionOptions_QuestionId_DisplayOrder"))
            {
                throw new ConflictException(
                    $"الترتيب {displayOrder} مستخدم بالفعل في السؤال رقم {questionId}", ex);
            }
            catch (DbUpdateException ex) when (
                ex.IsUniqueViolationOf("UQ_QuestionOptions_OneCorrectPerQuestion"))
            {
                throw new ConflictException(
                    $"السؤال رقم {questionId} له إجابة صحيحة بالفعل", ex);
            }
        }

        // Mirrors UQ_QuestionOptions_QuestionId_DisplayOrder.
        private async Task EnsureDisplayOrderIsFreeAsync(int questionId, short displayOrder, int? excludingOptionId, CancellationToken cancellationToken = default)
        {
            var taken = await _db.QuestionOptions
                .AsNoTracking()
                .AnyAsync(o => o.QuestionId == questionId
                            && o.DisplayOrder == displayOrder
                            && (excludingOptionId == null || o.Id != excludingOptionId.Value), cancellationToken: cancellationToken);

            if (taken)
                throw new InvalidOperationException(
                    $"الترتيب {displayOrder} مستخدم بالفعل في السؤال رقم {questionId}");
        }

        // Mirrors the filtered unique index UQ_QuestionOptions_OneCorrectPerQuestion.
        private async Task EnsureNoOtherCorrectOptionAsync(int questionId, int? excludingOptionId, CancellationToken cancellationToken = default)
        {
            var alreadyHasCorrect = await _db.QuestionOptions
                .AsNoTracking()
                .AnyAsync(o => o.QuestionId == questionId
                            && o.IsCorrect
                            && (excludingOptionId == null || o.Id != excludingOptionId.Value), cancellationToken: cancellationToken);

            if (alreadyHasCorrect)
                throw new InvalidOperationException(
                    $"السؤال رقم {questionId} له إجابة صحيحة بالفعل");
        }

        private static string NormalizeOptionText(string? optionText)
        {
            if (string.IsNullOrWhiteSpace(optionText))
                throw new ArgumentException("نص الاختيار مطلوب", nameof(optionText));

            return optionText.Trim();
        }

        private static AdminQuestionOptionResponseDto ToResponse(QuestionOption option) => new()
        {
            Id = option.Id,
            OptionText = option.OptionText,
            IsCorrect = option.IsCorrect,
            DisplayOrder = option.DisplayOrder
        };
    }
}
