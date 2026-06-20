CREATE DATABASE todo;
GO
USE todo;
GO
CREATE TABLE TodoItems( Id INT NOT NULL IDENTITY(1, 1) PRIMARY KEY, Title VARCHAR(255) NOT NULL, Body VARCHAR(MAX), CreatedAt DATETIME NOT NULL );
GO
INSERT INTO TodoItems( Title, Body, CreatedAt ) VALUES ( 'Buy groceries', 'Milk, Bread, Eggs, Butter', '2024-06-01 10:00:00' );
GO
INSERT INTO TodoItems( Title, Body, CreatedAt ) VALUES ( 'Call Mom', 'Check in and see how she is doing', '2024-06-02 15:30:00' );
GO
INSERT INTO TodoItems( Title, Body, CreatedAt ) VALUES ( 'Finish project report', 'Complete the final report for the project and submit it by the deadline', '2024-06-03 09:00:00' );
GO
INSERT INTO TodoItems( Title, Body, CreatedAt ) VALUES ( 'Workout', 'Go to the gym for a workout session', '2024-06-04 18:00:00' );
GO
INSERT INTO TodoItems( Title, Body, CreatedAt ) VALUES ( 'Read a book', 'Read at least 50 pages of the new novel', '2024-06-05 20:00:00' );
GO
