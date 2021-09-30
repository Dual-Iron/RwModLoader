namespace Mutator;

public enum ExitCodes
{
    InternalError = -1,
    Success = 0,
    InvalidArgs = 1,
    ConnectionFailed = 2,
    AbsentRelease = 3,
    AbsentBinaries = 4,
    InvalidReleaseTag = 5,
    AbsentFile = 6,
    AbsentRainWorldFolder = 7,
    InvalidRwmodType = 8,
    EmptyRwmod = 9,
    CorruptRwmod = 10,
    AbsentDependency = 11,
    RepoNotCompliant = 12,
    IOError = 13,
}
