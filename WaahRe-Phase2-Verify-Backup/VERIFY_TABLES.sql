-- Read-only check in SQL Server Management Studio.
-- Select your WaahReHistory database before executing.
-- This checks that Phase 2 SQL storage tables exist and contain records.
SELECT 'Jobs' AS Entity, COUNT_BIG(*) AS Total FROM dbo.Jobs
UNION ALL SELECT 'Analyses', COUNT_BIG(*) FROM dbo.Analyses
UNION ALL SELECT 'Drafts', COUNT_BIG(*) FROM dbo.Drafts
UNION ALL SELECT 'Archives', COUNT_BIG(*) FROM dbo.Archives;
-- Metadata and original encrypted file bodies are in different locations.
-- Do not delete App_Data or Data Protection keys after migrating to SQL.
