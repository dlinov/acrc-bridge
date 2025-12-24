// Explicitly configure MSTest parallelization to silence MSTEST0001.
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]


