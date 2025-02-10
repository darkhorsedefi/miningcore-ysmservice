using System;
using Autofac;

namespace Miningcore.Crypto.Hashing.Yescrypt
{
public interface IYescryptSolverFactory
{
IYescryptSolver CreateSolver(int N, int r, string personalization);
}

public class YescryptSolverFactory : IYescryptSolverFactory
{
    private readonly IComponentContext ctx;

    public YescryptSolverFactory(IComponentContext ctx)
    {
        this.ctx = ctx;
    }

    public IYescryptSolver CreateSolver(int N, int r, string personalization)
    {
        return new YescryptSolver(N, r, personalization);
    }
}
}
