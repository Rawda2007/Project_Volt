using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssessmentBL.DTOs.Question
{
        public class CreateQuestionDto
        {
            public int QuizId { get; set; }

            public int TopicId { get; set; }

            public string QuestionText { get; set; } = null!;

            public string Difficulty { get; set; } = null!;

            public short DisplayOrder { get; set; }

            public byte Points { get; set; }
        }
    }

