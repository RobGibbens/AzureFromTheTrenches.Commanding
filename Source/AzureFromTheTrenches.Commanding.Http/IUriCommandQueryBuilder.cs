﻿using System;

namespace AzureFromTheTrenches.Commanding.Http
{
    public interface IUriCommandQueryBuilder
    {
        string Query<TCommand>(Uri uri, TCommand command) where TCommand : class;
    }
}
