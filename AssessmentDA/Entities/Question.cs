using System;
using System.Collections.Generic;

namespace AssessmentDA.Entities;

public partial class Question
{
    public int Id { get; set; }

    public int QuizId { get; set; }

    public int TopicId { get; set; }

    public string QuestionText { get; set; } = null!;

    public string Difficulty { get; set; } = null!;

    public short DisplayOrder { get; set; }

    public byte Points { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<QuestionOption> QuestionOptions { get; set; }
        = new List<QuestionOption>();
    public virtual Quiz Quiz { get; set; } = null!;

    public virtual ICollection<QuizAttemptMistake> QuizAttemptMistakes { get; set; } = new List<QuizAttemptMistake>();

    public virtual Topic Topic { get; set; } = null!;
    public virtual ICollection<QuizAttemptQuestion> QuizAttemptQuestions { get; set; }
    = new List<QuizAttemptQuestion>();
}
