/*                 
 *     EntityFrameworkCore.FirebirdSqlSQL  - Congratulations EFCore Team
 *              https://www.FirebirdSqlsql.org/en/net-provider/ 
 *     Permission to use, copy, modify, and distribute this software and its
 *     documentation for any purpose, without fee, and without a written
 *     agreement is hereby granted, provided that the above copyright notice
 *     and this paragraph and the following two paragraphs appear in all copies. 
 * 
 *     The contents of this file are subject to the Initial
 *     Developer's Public License Version 1.0 (the "License");
 *     you may not use this file except in compliance with the
 *     License. You may obtain a copy of the License at
 *     http://www.FirebirdSqlsql.org/index.php?op=doc&id=idpl
 *
 *     Software distributed under the License is distributed on
 *     an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either
 *     express or implied.  See the License for the specific
 *     language governing rights and limitations under the License.
 *
 *              Copyright (c) 2017 Rafael Almeida
 *         Made In Sergipe-Brasil - ralms@ralms.net 
 *                  All Rights Reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Internal;


namespace Microsoft.EntityFrameworkCore.Update.Internal
{ 

    public class FirebirdSqlModificationCommandBatch : AffectedCountModificationCommandBatch
    {
        internal const int DefaultNetworkPacketSizeBytes = 4096;
        internal const int MaxScriptLength = 65536 * DefaultNetworkPacketSizeBytes / 2;
        internal const int MaxParameterCount = 2100;
        internal const int MaxRowCount = 256;
        internal int CountParameter = 1;
        internal readonly int _maxBatchSize;
        internal readonly List<ModificationCommand> _BlockInsertCommands = new List<ModificationCommand>();
        internal readonly List<ModificationCommand> _BlockUpdateCommands = new List<ModificationCommand>();
        internal readonly List<ModificationCommand> _BlockDeleteCommands = new List<ModificationCommand>();

        int _commandsLeftToLengthCheck = 50; 
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public FirebirdSqlModificationCommandBatch(
            [NotNull] IRelationalCommandBuilderFactory commandBuilderFactory,
            [NotNull] ISqlGenerationHelper sqlGenerationHelper,
            // ReSharper disable once SuggestBaseTypeForParameter
            [NotNull] IFirebirdSqlUpdateSqlGenerator updateSqlGenerator,
            [NotNull] IRelationalValueBufferFactoryFactory valueBufferFactoryFactory,
            [CanBeNull] int? maxBatchSize)
            : base( commandBuilderFactory, sqlGenerationHelper, updateSqlGenerator,valueBufferFactoryFactory)
        {
            if (maxBatchSize.HasValue && maxBatchSize.Value <= 0) 
                throw new ArgumentOutOfRangeException(nameof(maxBatchSize), RelationalStrings.InvalidMaxBatchSize);
           
            _maxBatchSize = Math.Min(maxBatchSize ?? int.MaxValue, MaxRowCount);
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected new virtual IFirebirdSqlUpdateSqlGenerator UpdateSqlGenerator => (IFirebirdSqlUpdateSqlGenerator)base.UpdateSqlGenerator;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override bool CanAddCommand(ModificationCommand modificationCommand)
        {
            if (ModificationCommands.Count >= _maxBatchSize) 
                return false;
          

            var additionalParameterCount = CountParameters(modificationCommand); 
            if (CountParameter + additionalParameterCount >= MaxParameterCount) 
                return false;
          

            CountParameter += additionalParameterCount;
            return true;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override bool IsCommandTextValid()
            =>  true;
        

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override int GetParameterCount()
            => CountParameter;

        private static int CountParameters(ModificationCommand modificationCommand)
        {
            var parameterCount = 0;
            foreach (var columnModification in modificationCommand.ColumnModifications)
            {
                if (columnModification.UseCurrentValueParameter) 
                    parameterCount++; 

                if (columnModification.UseOriginalValueParameter) 
                    parameterCount++;
                
            }

            return parameterCount;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override void ResetCommandText()
        {
            base.ResetCommandText();
            _BlockInsertCommands.Clear();
            _BlockUpdateCommands.Clear();
            _BlockDeleteCommands.Clear();
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override string GetCommandText()
            => base.GetCommandText() 
            + GetBlockInsertCommandText(ModificationCommands.Count) 
            + GetBlockUpdateCommandText(ModificationCommands.Count)
            + GetBlockDeleteCommandText(ModificationCommands.Count);

        private string GetBlockDeleteCommandText(int lastIndex)
        {
            if (_BlockDeleteCommands.Count == 0)
                return string.Empty;


            var stringBuilder = new StringBuilder();
            var resultSetMapping = UpdateSqlGenerator.AppendBlockDeleteOperation(stringBuilder, _BlockDeleteCommands, lastIndex - _BlockDeleteCommands.Count);
            for (var i = lastIndex - _BlockDeleteCommands.Count; i < lastIndex; i++)
                CommandResultSet[i] = resultSetMapping;

            if (resultSetMapping != ResultSetMapping.NoResultSet)
                CommandResultSet[lastIndex - 1] = ResultSetMapping.LastInResultSet;

            return stringBuilder.ToString();
        }

        private string GetBlockUpdateCommandText(int lastIndex)
        {
            if (_BlockUpdateCommands.Count == 0) 
                return string.Empty;
            

            var stringBuilder = new StringBuilder();
            var resultSetMapping = UpdateSqlGenerator.AppendBlockUpdateOperation(stringBuilder, _BlockUpdateCommands, lastIndex - _BlockUpdateCommands.Count);
            for (var i = lastIndex - _BlockUpdateCommands.Count; i < lastIndex; i++)
                CommandResultSet[i] = resultSetMapping; 

            if (resultSetMapping != ResultSetMapping.NoResultSet) 
                CommandResultSet[lastIndex - 1] = ResultSetMapping.LastInResultSet;
          
            return stringBuilder.ToString();
        }

        private string GetBlockInsertCommandText(int lastIndex)
        {
            if (_BlockInsertCommands.Count == 0) 
                return string.Empty;
       
            var stringBuilder = new StringBuilder();
            var resultSetMapping = UpdateSqlGenerator.AppendBlockInsertOperation(stringBuilder, _BlockInsertCommands, lastIndex - _BlockInsertCommands.Count);
            for (var i = lastIndex - _BlockInsertCommands.Count; i < lastIndex; i++)
                CommandResultSet[i] = resultSetMapping;
            

            if (resultSetMapping != ResultSetMapping.NoResultSet) 
                CommandResultSet[lastIndex - 1] = ResultSetMapping.LastInResultSet;
         
            
            return stringBuilder.ToString();
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override void UpdateCachedCommandText(int commandPosition)
        {
            var newModificationCommand = ModificationCommands[commandPosition];

            if (newModificationCommand.EntityState == EntityState.Added)
            {
                if (_BlockInsertCommands.Count > 0
                    && !CanBeInsertedInSameStatement(_BlockInsertCommands[0], newModificationCommand))
                {
                    CachedCommandText.Append(GetBlockInsertCommandText(commandPosition));
                    _BlockInsertCommands.Clear();
                }
                _BlockInsertCommands.Add(newModificationCommand); 
                LastCachedCommandIndex = commandPosition;
            }
            else if (newModificationCommand.EntityState == EntityState.Modified)
            {
                if (_BlockUpdateCommands.Count > 0
                    && !CanBeUpdateInSameStatement(_BlockUpdateCommands[0], newModificationCommand))
                {
                    CachedCommandText.Append(GetBlockUpdateCommandText(commandPosition));
                    _BlockUpdateCommands.Clear();
                }
                _BlockUpdateCommands.Add(newModificationCommand); 
                LastCachedCommandIndex = commandPosition;
            }
            else if (newModificationCommand.EntityState == EntityState.Deleted)
            {
                if (_BlockDeleteCommands.Count > 0
                    && !CanBeDeleteInSameStatement(_BlockDeleteCommands[0], newModificationCommand))
                {
                    CachedCommandText.Append(GetBlockDeleteCommandText(commandPosition));
                    _BlockDeleteCommands.Clear();
                }
                _BlockDeleteCommands.Add(newModificationCommand);
                LastCachedCommandIndex = commandPosition;
            }
            else
            {
                CachedCommandText.Append(GetBlockInsertCommandText(commandPosition));
                _BlockInsertCommands.Clear(); 
                base.UpdateCachedCommandText(commandPosition);
            }
        }

        private static bool CanBeDeleteInSameStatement(ModificationCommand firstCommand, ModificationCommand secondCommand)
  => string.Equals(firstCommand.TableName, secondCommand.TableName, StringComparison.Ordinal)
     && string.Equals(firstCommand.Schema, secondCommand.Schema, StringComparison.Ordinal)
     && firstCommand.ColumnModifications.Where(o => o.IsWrite).Select(o => o.ColumnName).SequenceEqual(
         secondCommand.ColumnModifications.Where(o => o.IsWrite).Select(o => o.ColumnName))
     && firstCommand.ColumnModifications.Where(o => o.IsRead).Select(o => o.ColumnName).SequenceEqual(
         secondCommand.ColumnModifications.Where(o => o.IsRead).Select(o => o.ColumnName));


        private static bool CanBeUpdateInSameStatement(ModificationCommand firstCommand, ModificationCommand secondCommand)
    => string.Equals(firstCommand.TableName, secondCommand.TableName, StringComparison.Ordinal)
       && string.Equals(firstCommand.Schema, secondCommand.Schema, StringComparison.Ordinal)
       && firstCommand.ColumnModifications.Where(o => o.IsWrite).Select(o => o.ColumnName).SequenceEqual(
           secondCommand.ColumnModifications.Where(o => o.IsWrite).Select(o => o.ColumnName))
       && firstCommand.ColumnModifications.Where(o => o.IsRead).Select(o => o.ColumnName).SequenceEqual(
           secondCommand.ColumnModifications.Where(o => o.IsRead).Select(o => o.ColumnName));

        private static bool CanBeInsertedInSameStatement(ModificationCommand firstCommand, ModificationCommand secondCommand)
            => string.Equals(firstCommand.TableName, secondCommand.TableName, StringComparison.Ordinal)
               && string.Equals(firstCommand.Schema, secondCommand.Schema, StringComparison.Ordinal)
               && firstCommand.ColumnModifications.Where(o => o.IsWrite).Select(o => o.ColumnName).SequenceEqual(
                   secondCommand.ColumnModifications.Where(o => o.IsWrite).Select(o => o.ColumnName))
               && firstCommand.ColumnModifications.Where(o => o.IsRead).Select(o => o.ColumnName).SequenceEqual(
                   secondCommand.ColumnModifications.Where(o => o.IsRead).Select(o => o.ColumnName));
    }
}
