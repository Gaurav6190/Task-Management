-- Removes exactly the rows inserted by SeedSampleTasks.sql (Id range 5EED0000-0000-0000-0000-0000000000NN).
-- Used by the 'SeedSampleTasks' migration's Down(). Never touches user-created tasks.
DELETE FROM dbo.Tasks
WHERE LEFT(CONVERT(char(36), Id), 32) = '5EED0000-0000-0000-0000-00000000';
