CREATE TABLE Users (
    Id TEXT PRIMARY KEY,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    IsActive INTEGER NOT NULL,
    FullName TEXT NOT NULL,
    Username TEXT NOT NULL UNIQUE,
    Email TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    PasswordSalt TEXT NOT NULL,
    Role INTEGER NOT NULL,
    LastLoginAtUtc TEXT NULL,
    MustChangePassword INTEGER NOT NULL
);
