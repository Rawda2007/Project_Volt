USE [VoltDB]
GO

/****** Object:  Table [Assessment].[QuizAttemptQuestions]    Script Date: 9/11/2026 2:25:53 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[QuizAttemptQuestions](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[QuizAttemptId] [bigint] NOT NULL,
	[QuestionId] [int] NOT NULL,
	[CreatedAt] [datetime2](3) NOT NULL,
	[TopicId] [int] NOT NULL,
	[Difficulty] [nvarchar](20) NOT NULL,
	[CorrectOptionId] [int] NOT NULL,
	[QuestionType] [nvarchar](30) NOT NULL,
 CONSTRAINT [PK_QuizAttemptQuestions] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_QuizAttemptQuestions_AttemptId_QuestionId] UNIQUE NONCLUSTERED 
(
	[QuizAttemptId] ASC,
	[QuestionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions] ADD  CONSTRAINT [DF_QuizAttemptQuestions_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions] ADD  CONSTRAINT [DF_QuizAttemptQuestions_QuestionType]  DEFAULT ('MultipleChoice') FOR [QuestionType]
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttemptQuestions_QuestionId_CorrectOptionId] FOREIGN KEY([QuestionId], [CorrectOptionId])
REFERENCES [Assessment].[QuestionOptions] ([QuestionId], [Id])
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions] CHECK CONSTRAINT [FK_QuizAttemptQuestions_QuestionId_CorrectOptionId]
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttemptQuestions_Questions] FOREIGN KEY([QuestionId])
REFERENCES [Assessment].[Questions] ([Id])
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions] CHECK CONSTRAINT [FK_QuizAttemptQuestions_Questions]
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttemptQuestions_QuizAttempts] FOREIGN KEY([QuizAttemptId])
REFERENCES [Assessment].[QuizAttempts] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions] CHECK CONSTRAINT [FK_QuizAttemptQuestions_QuizAttempts]
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttemptQuestions_Topics] FOREIGN KEY([TopicId])
REFERENCES [Assessment].[Topics] ([Id])
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions] CHECK CONSTRAINT [FK_QuizAttemptQuestions_Topics]
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttemptQuestions_Difficulty] CHECK  (([Difficulty]='Advanced' OR [Difficulty]='Hard' OR [Difficulty]='Medium' OR [Difficulty]='Easy'))
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions] CHECK CONSTRAINT [CK_QuizAttemptQuestions_Difficulty]
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttemptQuestions_EssayHasNoKey] CHECK  (([QuestionType]='Essay' AND [CorrectOptionId] IS NULL OR [QuestionType]<>'Essay' AND [CorrectOptionId] IS NOT NULL))
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions] CHECK CONSTRAINT [CK_QuizAttemptQuestions_EssayHasNoKey]
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions]  WITH CHECK ADD  CONSTRAINT [CK_QuizAttemptQuestions_QuestionType] CHECK  (([QuestionType]='Essay' OR [QuestionType]='TrueFalse' OR [QuestionType]='MultipleChoice'))
GO

ALTER TABLE [Assessment].[QuizAttemptQuestions] CHECK CONSTRAINT [CK_QuizAttemptQuestions_QuestionType]
GO


