using System;
using System.Collections.Generic;

namespace AssessmentDA.Entities;

public partial class QuestionOption
{
    public int Id { get; set; }

    public int QuestionId { get; set; }

    public string OptionText { get; set; } = null!;

    public bool IsCorrect { get; set; }

    public short DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Question Question { get; set; } = null!;

    public virtual ICollection<QuizAttemptMistake> QuizAttemptMistakes { get; set; } = new List<QuizAttemptMistake>();
}
