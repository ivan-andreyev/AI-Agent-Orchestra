# Migration Rollback Procedures - AddChatTables

## Overview
This document provides rollback procedures for the `AddChatTables` migration (20250922204129) that added ChatSessions and ChatMessages tables to the AI Agent Orchestra database.

## Migration Details
- **Migration ID**: 20250922204129_AddChatTables
- **Applied**: 2025-09-23 00:42:43 UTC
- **Tables Added**:
  - ChatSessions (with indexes)
  - ChatMessages (with foreign key to ChatSessions)
  - Full Orchestra schema (complete database initialization)

## Rollback Commands

### Immediate Rollback (Remove AddChatTables Migration)

```bash
# Navigate to API project
cd src/Orchestra.API

# Rollback to state before AddChatTables migration
dotnet ef database update 0 --context OrchestraDbContext

# Remove the migration file (optional, only if completely discarding)
dotnet ef migrations remove --context OrchestraDbContext
```

### Alternative: Rollback to Previous Migration

If there were previous migrations (currently this is the initial migration):

```bash
# List all migrations to identify target
dotnet ef migrations list --context OrchestraDbContext

# Rollback to specific migration (replace with target migration name)
dotnet ef database update [PreviousMigrationName] --context OrchestraDbContext
```

## Data Safety Procedures

### Before Rollback - Data Backup

```bash
# Create backup of current database
cp orchestra.db orchestra.db.backup_$(date +%Y%m%d_%H%M%S)

# Or use SQLite backup command if available
sqlite3 orchestra.db ".backup orchestra_backup_$(date +%Y%m%d_%H%M%S).db"
```

### After Rollback - Verification

```bash
# Verify rollback completed successfully
dotnet ef migrations list --context OrchestraDbContext

# Check database connection
dotnet ef database update --context OrchestraDbContext --dry-run
```

## Emergency Recovery Procedures

### If Rollback Fails

1. **Stop all services accessing the database**:
   ```bash
   # Stop any running Orchestra.API instances
   pkill -f "Orchestra.API"
   ```

2. **Restore from backup**:
   ```bash
   # Restore database from backup
   cp orchestra.db.backup_[timestamp] orchestra.db
   ```

3. **Reset migrations table** (if corrupted):
   ```bash
   # Connect to SQLite and reset migrations
   sqlite3 orchestra.db "DELETE FROM __EFMigrationsHistory WHERE MigrationId = '20250922204129_AddChatTables';"
   ```

### If Database Corruption Occurs

1. **Create new empty database**:
   ```bash
   # Backup corrupted database
   mv orchestra.db orchestra.db.corrupted_$(date +%Y%m%d_%H%M%S)

   # Create fresh database (will be empty)
   rm -f orchestra.db
   dotnet ef database update --context OrchestraDbContext
   ```

2. **Recover data from backup** (manual process):
   - Extract data from backup using SQLite tools
   - Import essential data to new database

## Testing Rollback Procedure

### Development Environment Test

```bash
# 1. Create test database
cp orchestra.db test_rollback.db

# 2. Test rollback with test database
export HANGFIRE_CONNECTION="test_rollback.db"
dotnet ef database update 0 --context OrchestraDbContext

# 3. Verify tables are removed
sqlite3 test_rollback.db ".tables"  # Should not show ChatSessions/ChatMessages

# 4. Clean up test database
rm test_rollback.db
```

## Post-Rollback Actions

### Code Changes Required After Rollback

1. **Remove Entity Models** (if discarding chat feature):
   ```bash
   # Remove chat entity files
   rm src/Orchestra.Core/Models/Chat/ChatSession.cs
   rm src/Orchestra.Core/Models/Chat/ChatMessage.cs
   rm src/Orchestra.Core/Models/Chat/MessageType.cs
   ```

2. **Update DbContext**:
   ```csharp
   // Remove from OrchestraDbContext.cs:
   // public DbSet<ChatSession> ChatSessions { get; set; } = null!;
   // public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
   // (and related configuration in OnModelCreating)
   ```

3. **Update Startup.cs** (if needed):
   - Remove chat-related service registrations if they were added

### Restart Services

```bash
# Restart API service
cd src/Orchestra.API
dotnet run

# Verify no chat-related errors in logs
```

## Validation Checklist

After successful rollback:

- [ ] Migration is removed from `__EFMigrationsHistory` table
- [ ] ChatSessions table does not exist
- [ ] ChatMessages table does not exist
- [ ] Application starts without Entity Framework errors
- [ ] No references to chat entities in code (if fully removing feature)
- [ ] Database backup is safely stored
- [ ] All services restart successfully

## Contact Information

If rollback procedures fail or unexpected issues occur:
- Check Entity Framework logs for detailed error information
- Ensure no applications are accessing the database during rollback
- Consider restoring from backup rather than attempting complex recovery

## Version History

- **v1.0** - 2025-09-23: Initial rollback procedures for AddChatTables migration