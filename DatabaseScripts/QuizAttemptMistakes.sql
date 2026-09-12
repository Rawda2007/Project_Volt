USE [VoltDB]
GO

/****** Object:  Table [Assessment].[QuizAttemptMistakes]    Script Date: 9/11/2026 2:22:25 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[QuizAttemptMistakes](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[QuizAttemptId] [bigint] NOT NULL,
	[QuestionId] [int] NOT NULL,
	[SelectedOptionId] [int] NOT NULL,
	[CreatedAt] [datetime2](3) NOT NULL,
 CONSTRAINT [PK_QuizAttemptMistakes] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_QuizAttemptMistakes_AttemptId_QuestionId] UNIQUE NONCLUSTERED 
(
	[QuizAttemptId] ASC,
	[QuestionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [Assessment].[QuizAttemptMistakes] ADD  CONSTRAINT [DF_QuizAttemptMistakes_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO

ALTER TABLE [Assessment].[QuizAttemptMistakes]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttemptMistakes_QuestionId_SelectedOptionId] FOREIGN KEY([QuestionId], [SelectedOptionId])
REFERENCES [Assessment].[QuestionOptions] ([QuestionId], [Id])
GO

ALTER TABLE [Assessment].[QuizAttemptMistakes] CHECK CONSTRAINT [FK_QuizAttemptMistakes_QuestionId_SelectedOptionId]
GO

ALTER TABLE [Assessment].[QuizAttemptMistakes]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttemptMistakes_Questions] FOREIGN KEY([QuestionId])
REFERENCES [Assessment].[Questions] ([Id])
GO

ALTER TABLE [Assessment].[QuizAttemptMistakes] CHECK CONSTRAINT [FK_QuizAttemptMistakes_Questions]
GO

ALTER TABLE [Assessment].[QuizAttemptMistakes]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttemptMistakes_QuizAttemptQuestions] FOREIGN KEY([QuizAttemptId], [QuestionId])
REFERENCES [Assessment].[QuizAttemptQuestions] ([QuizAttemptId], [QuestionId])
GO

ALTER TABLE [Assessment].[QuizAttemptMistakes] CHECK CONSTRAINT [FK_QuizAttemptMistakes_QuizAttemptQuestions]
GO

ALTER TABLE [Assessment].[QuizAttemptMistakes]  WITH CHECK ADD  CONSTRAINT [FK_QuizAttemptMistakes_QuizAttempts] FOREIGN KEY([QuizAttemptId])
REFERENCES [Assessment].[QuizAttempts] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Assessment].[QuizAttemptMistakes] CHECK CONSTRAINT [FK_QuizAttemptMistakes_QuizAttempts]
GO


