USE [VoltDB]
GO

/****** Object:  Table [Assessment].[QuestionOptions]    Script Date: 9/11/2026 2:18:56 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[QuestionOptions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[QuestionId] [int] NOT NULL,
	[OptionText] [nvarchar](max) NULL,
	[IsCorrect] [bit] NOT NULL,
	[DisplayOrder] [smallint] NOT NULL,
	[CreatedAt] [datetime2](3) NOT NULL,
	[ImageUrl] [nvarchar](max) NULL,
	[ImageDescription] [nvarchar](max) NULL,
 CONSTRAINT [PK_QuestionOptions] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_QuestionOptions_QuestionId_DisplayOrder] UNIQUE NONCLUSTERED 
(
	[QuestionId] ASC,
	[DisplayOrder] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_QuestionOptions_QuestionId_Id] UNIQUE NONCLUSTERED 
(
	[QuestionId] ASC,
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [Assessment].[QuestionOptions] ADD  CONSTRAINT [DF_QuestionOptions_IsCorrect]  DEFAULT ((0)) FOR [IsCorrect]
GO

ALTER TABLE [Assessment].[QuestionOptions] ADD  CONSTRAINT [DF_QuestionOptions_DisplayOrder]  DEFAULT ((0)) FOR [DisplayOrder]
GO

ALTER TABLE [Assessment].[QuestionOptions] ADD  CONSTRAINT [DF_QuestionOptions_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO

ALTER TABLE [Assessment].[QuestionOptions]  WITH CHECK ADD  CONSTRAINT [FK_QuestionOptions_Questions] FOREIGN KEY([QuestionId])
REFERENCES [Assessment].[Questions] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Assessment].[QuestionOptions] CHECK CONSTRAINT [FK_QuestionOptions_Questions]
GO

ALTER TABLE [Assessment].[QuestionOptions]  WITH CHECK ADD  CONSTRAINT [CK_QuestionOptions_ImageOptionHasDescription] CHECK  (([OptionText] IS NOT NULL AND ltrim(rtrim([OptionText]))<>'' OR [ImageDescription] IS NOT NULL AND ltrim(rtrim([ImageDescription]))<>''))
GO

ALTER TABLE [Assessment].[QuestionOptions] CHECK CONSTRAINT [CK_QuestionOptions_ImageOptionHasDescription]
GO

ALTER TABLE [Assessment].[QuestionOptions]  WITH CHECK ADD  CONSTRAINT [CK_QuestionOptions_TextOrImage] CHECK  (([OptionText] IS NOT NULL OR [ImageUrl] IS NOT NULL))
GO

ALTER TABLE [Assessment].[QuestionOptions] CHECK CONSTRAINT [CK_QuestionOptions_TextOrImage]
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Admin-authored semantic description of ImageUrl. Sent to the AI so it can reason about an image it cannot see. NEVER returned in a child-facing response.' , @level0type=N'SCHEMA',@level0name=N'Assessment', @level1type=N'TABLE',@level1name=N'QuestionOptions', @level2type=N'COLUMN',@level2name=N'ImageDescription'
GO


