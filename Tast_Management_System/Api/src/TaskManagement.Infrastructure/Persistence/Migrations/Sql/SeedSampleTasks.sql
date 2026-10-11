-- =============================================================================
-- Seed: 30 sample tasks (27 active + 3 soft-deleted)
-- Run by migration 'SeedSampleTasks' in EVERY environment (task.md T7a, D11/D14).
--
-- * Covers all 12 Status x Priority combinations among the active tasks.
-- * Dates are relative to the moment the migration runs (UTC), so the data
--   always looks current: CreatedDate within the last 60 days, some due dates
--   in the future and some overdue.
-- * Fixed Ids 5EED0000-0000-0000-0000-0000000000NN identify seed rows; the
--   migration's Down() removes exactly this range.
-- * Idempotent (WHERE NOT EXISTS), so re-running never duplicates rows.
-- * No GO / BEGIN TRAN: EF Core runs each migration as one batch inside its
--   own transaction.
--
-- Active tasks per status:   ToDo 10, InProgress 9, Done 8          = 27
-- Active tasks per priority: Low 7, Medium 8, High 7, Critical 5    = 27
-- =============================================================================

DECLARE @now datetime2(7) = SYSUTCDATETIME();

INSERT INTO dbo.Tasks
    (Id, Title, Description, Status, Priority, AssignedTo, DueDateUtc,
     CreatedDate, ModifiedDate, CompletedAtUtc, IsDeleted, DeletedDate)
SELECT
    CONVERT(uniqueidentifier, CONCAT('5EED0000-0000-0000-0000-0000000000', RIGHT(CONCAT('0', v.Nr), 2))),
    v.Title,
    v.Description,
    v.Status,
    v.Priority,
    v.AssignedTo,
    -- Due dates are whole days (midnight UTC).
    CASE WHEN v.DueInDays IS NULL THEN NULL
         ELSE CAST(CAST(DATEADD(DAY, v.DueInDays, @now) AS date) AS datetime2(7)) END,
    DATEADD(DAY, -v.CreatedDaysAgo, @now),
    -- ModifiedDate: set for anything that was touched after creation (not ToDo, or deleted).
    CASE WHEN v.UpdatedDaysAgo IS NULL THEN NULL ELSE DATEADD(DAY, -v.UpdatedDaysAgo, @now) END,
    -- CompletedAtUtc: set for Done tasks only.
    CASE WHEN v.Status = N'Done' THEN DATEADD(DAY, -v.UpdatedDaysAgo, @now) END,
    v.IsDeleted,
    CASE WHEN v.IsDeleted = 1 THEN DATEADD(DAY, -v.UpdatedDaysAgo, @now) END
FROM (VALUES
    -- Nr, Title, Description, Status, Priority, AssignedTo, DueInDays, CreatedDaysAgo, UpdatedDaysAgo, IsDeleted

    -- ---- ToDo (10) -----------------------------------------------------------
    ( 1, N'Set up CI pipeline for the API',            N'Build, test and publish on every push to main.',                     N'ToDo',       N'High',     N'alice', 14,    5,  NULL, 0),
    ( 2, N'Write onboarding guide for new developers', N'Cover local setup, SQL Server, migrations and running the tests.',   N'ToDo',       N'Low',      NULL,     30,   12,  NULL, 0),
    ( 3, N'Add pagination to audit log export',        N'Large exports time out; page the results in batches of 500.',       N'ToDo',       N'Medium',   N'bob',   21,    8,  NULL, 0),
    ( 4, N'Investigate slow task list query',          N'p95 of GET /api/tasks is above 800 ms with 10k rows.',              N'ToDo',       N'Critical', N'carol',  3,    2,  NULL, 0),
    ( 5, N'Update third-party license list',           N'Review package licenses before the next release.',                  N'ToDo',       N'Low',      N'dave',  NULL, 40,  NULL, 0),
    ( 6, N'Design dark mode for the dashboard',        N'Prepare mockups for the dashboard and task detail pages.',          N'ToDo',       N'Medium',   NULL,     45,   15,  NULL, 0),
    ( 7, N'Rotate SQL Server service account password',N'Scheduled quarterly rotation for the reporting server.',            N'ToDo',       N'Critical', N'alice', -2,   20,  NULL, 0),
    ( 8, N'Add due-date reminder emails',              N'Email the assignee one day before the due date.',                   N'ToDo',       N'High',     N'bob',   28,   10,  NULL, 0),
    ( 9, N'Clean up unused feature flags',             N'Remove flags that have been fully rolled out.',                     N'ToDo',       N'Low',      NULL,     NULL, 55,  NULL, 0),
    (10, N'Draft Q1 roadmap',                          N'Collect input from support and sales.',                             N'ToDo',       N'Medium',   N'carol', 10,    3,  NULL, 0),

    -- ---- InProgress (9) -------------------------------------------------------
    (11, N'Fix login timeout on mobile',               N'Sessions expire after 5 minutes on iOS instead of 30.',             N'InProgress', N'Critical', N'alice',  1,    9,     1, 0),
    (12, N'Migrate file attachments to blob storage',  N'Move attachments from the file share to blob containers.',         N'InProgress', N'High',     N'dave',  20,   25,     4, 0),
    (13, N'Improve validation messages on task form',  N'Show the field-level errors returned by the API.',                  N'InProgress', N'Medium',   N'bob',    7,   11,     2, 0),
    (14, N'Refactor notification service',             N'Split email and push delivery into separate handlers.',             N'InProgress', N'Medium',   N'carol', NULL, 18,     6, 0),
    (15, N'Add Swagger examples for task endpoints',   N'Request and response examples for create and update.',              N'InProgress', N'Low',      N'dave',  12,   14,     3, 0),
    (16, N'Resolve deadlock in nightly import job',    N'Import and cleanup jobs lock the Tasks table in opposite order.',   N'InProgress', N'Critical', N'bob',   -1,    6,     1, 0),
    (17, N'Review accessibility of the task board',    N'Keyboard navigation and screen reader labels.',                     N'InProgress', N'Medium',   N'alice', 15,   22,     5, 0),
    (18, N'Switch logging to structured JSON',         N'Include traceId and error code in every log entry.',                N'InProgress', N'High',     N'carol',  9,   13,     2, 0),
    (19, N'Tidy up README badges',                     N'Replace the broken build and coverage badges.',                     N'InProgress', N'Low',      N'alice', NULL, 30,     7, 0),

    -- ---- Done (8) ---------------------------------------------------------------
    (20, N'Create Tasks table migration',              N'Initial EF Core code-first migration for the Tasks table.',         N'Done',       N'High',     N'alice', NULL, 50,    45, 0),
    (21, N'Implement soft delete for tasks',           N'DELETE marks tasks as deleted instead of removing them.',           N'Done',       N'High',     N'bob',   NULL, 35,    30, 0),
    (22, N'Add global exception handler',              N'Consistent ProblemDetails responses with error codes.',             N'Done',       N'Critical', N'carol', NULL, 28,    20, 0),
    (23, N'Set up local SQL Server with Windows auth', N'Developer machines use the default instance on localhost.',         N'Done',       N'Medium',   N'dave',  NULL, 24,    21, 0),
    (24, N'Write unit tests for task validators',      N'Cover the title, status and priority rules.',                       N'Done',       N'Medium',   N'alice', NULL, 19,    12, 0),
    (25, N'Fix typo in welcome email',                 N'Wrong product name in the subject line.',                           N'Done',       N'Low',      NULL,     NULL, 33,    32, 0),
    (26, N'Archive old sprint boards',                 N'Move boards older than one year to the archive.',                   N'Done',       N'Low',      N'bob',   NULL, 58,    50, 0),
    (27, N'Add filtering by status and priority',      N'GET /api/tasks supports status and priority query parameters.',     N'Done',       N'High',     N'carol', NULL, 16,     9, 0),

    -- ---- Soft-deleted (3): stay in the table, never returned or counted ----------
    (28, N'Duplicate: fix login timeout',              N'Duplicate of "Fix login timeout on mobile".',                       N'ToDo',       N'Low',      NULL,     NULL,  9,     8, 1),
    (29, N'Evaluate legacy reporting tool',            N'Cancelled: the tool was retired.',                                  N'Done',       N'Medium',   N'dave',  NULL, 45,    40, 1),
    (30, N'Prototype chat integration',                N'Out of scope for this quarter.',                                    N'InProgress', N'High',     N'alice', 30,   20,    15, 1)
) AS v (Nr, Title, Description, Status, Priority, AssignedTo, DueInDays, CreatedDaysAgo, UpdatedDaysAgo, IsDeleted)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Tasks AS t
    WHERE t.Id = CONVERT(uniqueidentifier, CONCAT('5EED0000-0000-0000-0000-0000000000', RIGHT(CONCAT('0', v.Nr), 2)))
);
