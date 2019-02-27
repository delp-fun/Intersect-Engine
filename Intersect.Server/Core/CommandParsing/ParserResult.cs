﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Intersect.Server.Core.CommandParsing.Arguments;
using Intersect.Server.Core.CommandParsing.Commands;
using Intersect.Server.Core.CommandParsing.Errors;
using JetBrains.Annotations;

namespace Intersect.Server.Core.CommandParsing
{
    public class ParserResult<TCommand>
        where TCommand : ICommand
    {
        [CanBeNull]
        public TCommand Command { get; }

        [NotNull]
        public ArgumentValuesMap Parsed { get; }

        [NotNull]
        public ImmutableList<UnhandledArgumentError> Unhandled { get; }

        [NotNull]
        public ImmutableList<ParserError> Errors { get; }

        public ParserResult(
            [NotNull] ArgumentValuesMap parsed,
            [CanBeNull] IEnumerable<ParserError> errors = null
        ) : this(default(TCommand), parsed, errors)
        {
        }

        public ParserResult(
            [CanBeNull] TCommand command,
            [NotNull] ArgumentValuesMap parsed,
            [CanBeNull] IEnumerable<ParserError> errors = null
        )
        {
            Command = command;
            Parsed = parsed;
            Errors = (
                         errors?.ToImmutableList() ??
                         ImmutableList.Create<ParserError>()
                     ) ?? throw new InvalidOperationException();
            Unhandled = Errors
                            .Where(error => error is UnhandledArgumentError)
                            .Cast<UnhandledArgumentError>()
                            .ToImmutableList() ??
                        throw new InvalidOperationException();
        }

        public ParserResult(
            [CanBeNull] TCommand command,
            [NotNull] ParserError error
        ) : this(command, new ArgumentValuesMap(), new[] {error})
        {
        }
    }

    public class ParserResult
        : ParserResult<ICommand>
    {
        public ParserResult(
            [NotNull] ArgumentValuesMap parsed,
            [CanBeNull] IEnumerable<ParserError> errors = null
        ) : base(parsed, errors)
        {
        }

        public ParserResult(
            [CanBeNull] ICommand command,
            [NotNull] ArgumentValuesMap parsed,
            [CanBeNull] IEnumerable<ParserError> errors = null
        ) : base(command, parsed, errors)
        {
        }

        public ParserResult(
            [CanBeNull] ICommand command,
            [NotNull] ParserError error
        ) : base(command, error)
        {
        }
    }
}