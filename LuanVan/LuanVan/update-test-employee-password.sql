SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON
GO
UPDATE AspNetUsers
SET PasswordHash = '$2a$11$M7EasOoQPmCwyXjst46hTONxYmr.Akr8u0dwAHm1a57MvnfmCzc/u'
WHERE Id = 'test-employee-001';
GO
