/*
SQL Server 2022. SELECT metadata only, no GO, no USE, no persistent DDL/DML.
Run using Export-Schema.ps1 or on a DEV copy selected explicitly in SSMS.
Requires VIEW DEFINITION for the selected database. No business table rows exported.
T-SQL procedure parameter defaults must be read in module definitions; sys.parameters
does not reliably expose defaults for T-SQL procedures.
maxLengthBytes is bytes (nvarchar character capacity is normally half that value).
*/
SET NOCOUNT ON;
IF DB_NAME() NOT IN (N'FashionStoreDB_Dev', N'YodyStore_Dev')
    THROW 51000, N'Use only an already restored FashionStoreDB_Dev or YodyStore_Dev copy.', 1;
IF ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'VIEW DEFINITION'), 0) <> 1
    THROW 51001, N'VIEW DEFINITION is required to avoid an incomplete metadata export.', 1;

SELECT (
    SELECT
        1 AS exportFormatVersion,
        DB_NAME() AS databaseName,
        CONVERT(nvarchar(128), SERVERPROPERTY(N'ProductVersion')) AS engineVersion,
        CONVERT(varchar(33), SYSUTCDATETIME(), 126) AS exportedAtUtc,
        d.compatibility_level AS compatibilityLevel,
        d.collation_name AS databaseCollation,
        JSON_QUERY((
            SELECT
                (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0) AS tables,
                (SELECT COUNT(*) FROM sys.views WHERE is_ms_shipped = 0) AS views,
                (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_ms_shipped = 0) AS foreignKeys,
                (SELECT COUNT(*) FROM sys.procedures WHERE is_ms_shipped = 0) AS procedures,
                (SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled = 1 OR is_not_trusted = 1)
                  + (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1)
                    AS disabledOrUntrustedConstraints,
                (SELECT COUNT(*) FROM sys.objects o
                 LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
                 WHERE o.is_ms_shipped = 0 AND o.type IN ('P','V','FN','IF','TF','TR')
                   AND m.definition IS NULL) AS unavailableModuleDefinitions
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
        )) AS counts,
        JSON_QUERY((
            SELECT s.name AS [schema], t.name AS [name],
                t.temporal_type_desc AS temporalType, t.is_memory_optimized AS memoryOptimized
            FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name
            FOR JSON PATH, INCLUDE_NULL_VALUES
        )) AS tables,
        JSON_QUERY((
            SELECT s.name AS [schema], o.name AS [object], o.type AS objectType,
                c.column_id AS ordinal, c.name AS [name], ts.name AS typeSchema,
                ty.name AS typeName, c.max_length AS maxLengthBytes,
                c.precision AS [precision], c.scale AS scale, c.is_nullable AS nullable,
                c.collation_name AS collation, c.is_identity AS [identity],
                CONVERT(nvarchar(100), ic.seed_value) AS identitySeed,
                CONVERT(nvarchar(100), ic.increment_value) AS identityIncrement,
                c.is_computed AS computed, cc.definition AS computedDefinition,
                cc.is_persisted AS computedPersisted, dc.definition AS defaultDefinition,
                c.generated_always_type_desc AS generatedAlwaysType
            FROM sys.columns c
            JOIN sys.objects o ON o.object_id = c.object_id
            JOIN sys.schemas s ON s.schema_id = o.schema_id
            JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            JOIN sys.schemas ts ON ts.schema_id = ty.schema_id
            LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
            LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
            WHERE o.is_ms_shipped = 0 AND o.type IN ('U','V')
            ORDER BY s.name, o.name, c.column_id
            FOR JSON PATH, INCLUDE_NULL_VALUES
        )) AS columns,
        JSON_QUERY((
            SELECT s.name AS [schema], t.name AS [table], kc.name AS [name],
                kc.type AS keyType, c.name AS [column], ix.key_ordinal AS ordinal,
                ix.is_descending_key AS descending
            FROM sys.key_constraints kc
            JOIN sys.tables t ON t.object_id = kc.parent_object_id
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            JOIN sys.index_columns ix ON ix.object_id = kc.parent_object_id
                AND ix.index_id = kc.unique_index_id AND ix.key_ordinal > 0
            JOIN sys.columns c ON c.object_id = ix.object_id AND c.column_id = ix.column_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name, kc.name, ix.key_ordinal
            FOR JSON PATH, INCLUDE_NULL_VALUES
        )) AS keys,
        JSON_QUERY((
            SELECT ps.name AS [schema], pt.name AS [table], fk.name AS [name],
                fc.constraint_column_id AS ordinal, pc.name AS [column],
                rs.name AS referencedSchema, rt.name AS referencedTable, rc.name AS referencedColumn,
                fk.delete_referential_action_desc AS onDelete,
                fk.update_referential_action_desc AS onUpdate,
                fk.is_disabled AS disabled, fk.is_not_trusted AS untrusted,
                fk.is_not_for_replication AS notForReplication
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = fk.object_id
            JOIN sys.tables pt ON pt.object_id = fk.parent_object_id
            JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
            JOIN sys.columns pc ON pc.object_id = fc.parent_object_id AND pc.column_id = fc.parent_column_id
            JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
            JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
            JOIN sys.columns rc ON rc.object_id = fc.referenced_object_id AND rc.column_id = fc.referenced_column_id
            WHERE fk.is_ms_shipped = 0
            ORDER BY ps.name, pt.name, fk.name, fc.constraint_column_id
            FOR JSON PATH, INCLUDE_NULL_VALUES
        )) AS foreignKeys,
        JSON_QUERY((
            SELECT s.name AS [schema], t.name AS [table], ck.name AS [name], ck.definition,
                ck.is_disabled AS disabled, ck.is_not_trusted AS untrusted,
                ck.is_not_for_replication AS notForReplication
            FROM sys.check_constraints ck
            JOIN sys.tables t ON t.object_id = ck.parent_object_id
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name, ck.name
            FOR JSON PATH, INCLUDE_NULL_VALUES
        )) AS checks,
        JSON_QUERY((
            SELECT s.name AS [schema], o.name AS [object], i.name AS [name], i.type_desc AS indexType,
                i.is_unique AS [unique], i.is_primary_key AS primaryKey,
                i.is_unique_constraint AS uniqueConstraint, i.is_disabled AS disabled,
                i.filter_definition AS filterDefinition,
                JSON_QUERY((
                    SELECT c.name AS [column], ic.index_column_id AS ordinal,
                        ic.key_ordinal AS keyOrdinal, ic.is_descending_key AS descending,
                        ic.is_included_column AS included, ic.partition_ordinal AS partitionOrdinal
                    FROM sys.index_columns ic
                    LEFT JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                    WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                    ORDER BY ic.index_column_id
                    FOR JSON PATH, INCLUDE_NULL_VALUES
                )) AS columns
            FROM sys.indexes i
            JOIN sys.objects o ON o.object_id = i.object_id
            JOIN sys.schemas s ON s.schema_id = o.schema_id
            WHERE o.is_ms_shipped = 0 AND o.type IN ('U','V') AND i.index_id > 0 AND i.is_hypothetical = 0
            ORDER BY s.name, o.name, i.name
            FOR JSON PATH, INCLUDE_NULL_VALUES
        )) AS indexes,
        JSON_QUERY((
            SELECT s.name AS [schema], o.name AS [name], o.type AS objectType,
                m.definition, m.uses_ansi_nulls AS usesAnsiNulls,
                m.uses_quoted_identifier AS usesQuotedIdentifier,
                m.is_schema_bound AS schemaBound, m.execute_as_principal_id AS executeAsPrincipalId,
                tr.is_disabled AS triggerDisabled, tr.is_instead_of_trigger AS insteadOfTrigger
            FROM sys.objects o
            JOIN sys.schemas s ON s.schema_id = o.schema_id
            LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
            LEFT JOIN sys.triggers tr ON tr.object_id = o.object_id
            WHERE o.is_ms_shipped = 0 AND o.type IN ('P','V','FN','IF','TF','TR','PC','FS','FT')
            ORDER BY s.name, o.name
            FOR JSON PATH, INCLUDE_NULL_VALUES
        )) AS modules,
        JSON_QUERY((
            SELECT s.name AS [schema], o.name AS [object], p.parameter_id AS ordinal,
                p.name AS [name], ts.name AS typeSchema, ty.name AS typeName,
                p.max_length AS maxLengthBytes, p.precision AS [precision], p.scale AS scale,
                p.is_output AS output, p.is_readonly AS [readOnly]
            FROM sys.parameters p
            JOIN sys.objects o ON o.object_id = p.object_id
            JOIN sys.schemas s ON s.schema_id = o.schema_id
            JOIN sys.types ty ON ty.user_type_id = p.user_type_id
            JOIN sys.schemas ts ON ts.schema_id = ty.schema_id
            WHERE o.is_ms_shipped = 0
            ORDER BY s.name, o.name, p.parameter_id
            FOR JSON PATH, INCLUDE_NULL_VALUES
        )) AS parameters,
        JSON_QUERY((
            SELECT s.name AS [schema], ty.name AS [name], ty.is_table_type AS tableType,
                ty.system_type_id AS baseSystemTypeId, ty.max_length AS maxLengthBytes,
                ty.precision AS [precision], ty.scale AS scale, ty.is_nullable AS nullable
            FROM sys.types ty JOIN sys.schemas s ON s.schema_id = ty.schema_id
            WHERE ty.is_user_defined = 1
            ORDER BY s.name, ty.name
            FOR JSON PATH, INCLUDE_NULL_VALUES
        )) AS userTypes,
        JSON_QUERY((
            SELECT s.name AS [schema], sy.name AS [name], sy.base_object_name AS baseObject
            FROM sys.synonyms sy JOIN sys.schemas s ON s.schema_id = sy.schema_id
            ORDER BY s.name, sy.name
            FOR JSON PATH, INCLUDE_NULL_VALUES
        )) AS synonyms
    FROM sys.databases d
    WHERE d.database_id = DB_ID()
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
) AS SchemaJson;
