using System;
using System.Collections.Generic;

namespace AssessmentDA.Entities;

public partial class QuizAttemptQuestion
{
    public long Id { get; set; }

    public long QuizAttemptId { get; set; }

    public int QuestionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual QuizAttempt QuizAttempt { get; set; } = null!;

    public virtual Question Question { get; set; } = null!;
    public virtual QuizAttemptMistake? QuizAttemptMistake { get; set; }
}