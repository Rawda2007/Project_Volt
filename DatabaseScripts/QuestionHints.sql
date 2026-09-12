USE [VoltDB]
GO

/****** Object:  Table [Assessment].[QuestionHints]    Script Date: 9/11/2026 2:18:12 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[QuestionHints](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[QuizAttemptMistakeId] [bigint] NOT NULL,
	[HintText] [nvarchar](max) NOT NULL,
	[HintSequence] [tinyint] NOT NULL,
	[GeneratedAt] [datetime2](3) NOT NULL,
	[LanguageCode] [nvarchar](5) NOT NULL,
 CONSTRAINT [PK_QuestionHints] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_QuestionHints_MistakeId_Language_Sequence] UNIQUE NONCLUSTERED 
(
	[QuizAttemptMistakeId] ASC,
	[LanguageCode] ASC,
	[HintSequence] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [Assessment].[QuestionHints] ADD  CONSTRAINT [DF_QuestionHints_GeneratedAt]  DEFAULT (sysutcdatetime()) FOR [GeneratedAt]
GO

ALTER TABLE [Assessment].[QuestionHints] ADD  CONSTRAINT [DF_QuestionHints_LanguageCode]  DEFAULT (N'ar') FOR [LanguageCode]
GO

ALTER TABLE [Assessment].[QuestionHints]  WITH CHECK ADD  CONSTRAINT [FK_QuestionHints_Languages] FOREIGN KEY([LanguageCode])
REFERENCES [Assessment].[Languages] ([Code])
GO

ALTER TABLE [Assessment].[QuestionHints] CHECK CONSTRAINT [FK_QuestionHints_Languages]
GO

ALTER TABLE [Assessment].[QuestionHints]  WITH CHECK ADD  CONSTRAINT [FK_QuestionHints_QuizAttemptMistakes] FOREIGN KEY([QuizAttemptMistakeId])
REFERENCES [Assessment].[QuizAttemptMistakes] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Assessment].[QuestionHints] CHECK CONSTRAINT [FK_QuestionHints_QuizAttemptMistakes]
GO

ALTER TABLE [Assessment].[QuestionHints]  WITH CHECK ADD  CONSTRAINT [CK_QuestionHints_HintSequence] CHECK  (([HintSequence]>(0)))
GO

ALTER TABLE [Assessment].[QuestionHints] CHECK CONSTRAINT [CK_QuestionHints_HintSequence]
GO


