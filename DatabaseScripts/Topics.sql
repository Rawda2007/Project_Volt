USE [VoltDB]
GO

/****** Object:  Table [Assessment].[Topics]    Script Date: 9/11/2026 2:29:11 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[Topics](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Description] [nvarchar](max) NULL,
	[CategoryId] [tinyint] NOT NULL,
	[LearningLevel] [nvarchar](20) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](3) NOT NULL,
	[UpdatedAt] [datetime2](3) NULL,
 CONSTRAINT [PK_Topics] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Topics_Name] UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [Assessment].[Topics] ADD  CONSTRAINT [DF_Topics_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO

ALTER TABLE [Assessment].[Topics] ADD  CONSTRAINT [DF_Topics_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO

ALTER TABLE [Assessment].[Topics]  WITH CHECK ADD  CONSTRAINT [FK_Topics_Categories] FOREIGN KEY([CategoryId])
REFERENCES [Assessment].[Categories] ([Id])
GO

ALTER TABLE [Assessment].[Topics] CHECK CONSTRAINT [FK_Topics_Categories]
GO

ALTER TABLE [Assessment].[Topics]  WITH CHECK ADD  CONSTRAINT [CK_Topics_LearningLevel] CHECK  (([LearningLevel]='Advanced' OR [LearningLevel]='Intermediate' OR [LearningLevel]='Beginner'))
GO

ALTER TABLE [Assessment].[Topics] CHECK CONSTRAINT [CK_Topics_LearningLevel]
GO


