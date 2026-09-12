USE [VoltDB]
GO

/****** Object:  Table [Assessment].[QuizTranslations]    Script Date: 9/11/2026 2:27:22 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[QuizTranslations](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[QuizId] [int] NOT NULL,
	[LanguageCode] [nvarchar](5) NOT NULL,
	[Title] [nvarchar](300) NOT NULL,
	[Description] [nvarchar](max) NULL,
 CONSTRAINT [PK_QuizTranslations] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_QuizTranslations_QuizId_Language] UNIQUE NONCLUSTERED 
(
	[QuizId] ASC,
	[LanguageCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [Assessment].[QuizTranslations]  WITH CHECK ADD  CONSTRAINT [FK_QuizTranslations_Languages] FOREIGN KEY([LanguageCode])
REFERENCES [Assessment].[Languages] ([Code])
GO

ALTER TABLE [Assessment].[QuizTranslations] CHECK CONSTRAINT [FK_QuizTranslations_Languages]
GO

ALTER TABLE [Assessment].[QuizTranslations]  WITH CHECK ADD  CONSTRAINT [FK_QuizTranslations_Quizzes] FOREIGN KEY([QuizId])
REFERENCES [Assessment].[Quizzes] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Assessment].[QuizTranslations] CHECK CONSTRAINT [FK_QuizTranslations_Quizzes]
GO


