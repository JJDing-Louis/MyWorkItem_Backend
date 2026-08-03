:on error exit

IF SUSER_ID(N'myworkitem') IS NULL
BEGIN
    CREATE LOGIN [myworkitem]
        WITH PASSWORD = N'$(AppPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
END
ELSE
BEGIN
    ALTER LOGIN [myworkitem]
        WITH PASSWORD = N'$(AppPassword)', CHECK_POLICY = OFF;
END;

IF IS_SRVROLEMEMBER(N'sysadmin', N'myworkitem') <> 1
BEGIN
    ALTER SERVER ROLE [sysadmin] ADD MEMBER [myworkitem];
END;
