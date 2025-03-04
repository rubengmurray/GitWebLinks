using DotLiquid;
using Moq;
using System.Text.RegularExpressions;

namespace GitWebLinks;

public static class LinkHandlerTests {

    private static readonly Template EmptyTemplate = Template.Parse("");


    public class CreateUrlAsyncMethod : RepositoryTestBase {

        private readonly Mock<ISettings> _settings;
        private Repository _repository;


        static CreateUrlAsyncMethod() {
            TemplateEngine.Initialize();
        }


        public CreateUrlAsyncMethod() {
            _settings = new Mock<ISettings>();

            _repository = new Repository(
                RootDirectory,
                new Remote("origin", "http://example.com")
            );
        }


        [Theory]
        [InlineData(LinkType.Commit, "commit")]
        [InlineData(LinkType.CurrentBranch, "branch")]
        public async Task ShouldUseTheDefaultLinkTypeIfNoTypeWasSpecified(LinkType type, string expected) {
            await SetupRepositoryAsync(RootDirectory);

            _settings.Setup((x) => x.GetDefaultLinkTypeAsync()).ReturnsAsync(() => type);

            Assert.Equal(
                expected,
                await CreateUrlAsync(
                    new PartialHandlerDefinition { Url = "{{ type }}" },
                    null
                )
            );
        }


        [Fact]
        public async Task ShouldUseTheFullCommitHashAsTheRefValueWhenTheLinkTypeIsCommitAndShortHashesShouldNotBeUsed() {
            await SetupRepositoryAsync(RootDirectory);

            _settings.Setup((x) => x.GetUseShortHashesAsync()).ReturnsAsync(false);

            Assert.Equal(
                string.Concat(await Git.ExecuteAsync(RootDirectory, "rev-parse", "HEAD")).Trim(),
                await CreateUrlAsync(new PartialHandlerDefinition { Url = "{{ ref }}" }, LinkType.Commit)
            );
        }


        [Fact]
        public async Task ShouldUseTheShortCommitHashAsTheRefValueWhenTheLinkTypeIsCommitAndShortHashesShouldBeUsed() {
            await SetupRepositoryAsync(RootDirectory);

            _settings.Setup((x) => x.GetUseShortHashesAsync()).ReturnsAsync(true);

            Assert.Equal(
                string.Concat(await Git.ExecuteAsync(RootDirectory, "rev-parse", "--short", "HEAD")).Trim(),
                await CreateUrlAsync(new PartialHandlerDefinition { Url = "{{ ref }}" }, LinkType.Commit)
            );
        }


        [Fact]
        public async Task ShouldUseTheBranchNameAsTheRefValueWhenTheLinkTypeIsCurrentBranch() {
            await SetupRepositoryAsync(RootDirectory);

            await Git.ExecuteAsync(RootDirectory, "checkout", "-b", "foo");

            Assert.Equal(
                "foo",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Url = "{{ ref }}",
                        BranchRef = BranchRefType.Abbreviated
                    },
                    LinkType.CurrentBranch
                )
            );
        }


        [Fact]
        public async Task ShouldUseTheDefaultBranchNameAsTheRefValueWhenTheLinkTypeIsDefaultBranchAndDefaultBranchIsSpecified() {
            _settings.Setup((x) => x.GetDefaultBranchAsync()).ReturnsAsync("bar");

            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "bar",
                await CreateUrlAsync(
                    new PartialHandlerDefinition { Url = "{{ ref }}" },
                    LinkType.DefaultBranch
                )
            );
        }


        [Fact]
        public async Task ShouldThrowErrorWhenTheLinkTypeIsDefaultBranchAndTheRemoteDoesNotHaveHeadRef() {
            string origin;
            string repo;


            _settings.Setup((x) => x.GetDefaultBranchAsync()).ReturnsAsync("");

            origin = CreateDirectory("origin");
            repo = CreateDirectory("repo");

            await SetupRepositoryAsync(repo);
            await SetupRemoteAsync(repo, origin, "origin");

            SetRepositoryRoot(repo);

            await Assert.ThrowsAsync<NoRemoteHeadException>(
                () => CreateUrlAsync(
                    new PartialHandlerDefinition { Url = "{{ ref }}" },
                    LinkType.DefaultBranch
                )
            );
        }


        [Fact]
        public async Task ShouldUseTheDefaultBranchOfTheRemoteAsTheRefValueWhenTheLinkTypeIsDefaultBranchAndDefaultBranchIsNotSpecified() {
            string origin;
            string repo;


            _settings.Setup((x) => x.GetDefaultBranchAsync()).ReturnsAsync("");

            origin = CreateDirectory("origin");
            repo = CreateDirectory("repo");

            await SetupRepositoryAsync(repo);
            await Git.ExecuteAsync(repo, "checkout", "-b", "foo");
            await SetupRemoteAsync(repo, origin, "origin");

            await Git.ExecuteAsync(repo, "remote", "set-head", "origin", "master");

            SetRepositoryRoot(repo);

            Assert.Equal(
                "master",
                await CreateUrlAsync(
                    new PartialHandlerDefinition { Url = "{{ ref }}", BranchRef = BranchRefType.Abbreviated },
                    LinkType.DefaultBranch
                )
            );
        }


        [Fact]
        public async Task ShouldHandleTheMatchingServerHttpAddressEndingWithSlash() {
            SetRemoteUrl("http://example.com/foo/bar");

            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com | foo/bar",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Server = new StaticServer("http://example.com/", ""),
                        Url = "{{ base }} | {{ repository }}"
                    },
                    null
                )
            );
        }


        [Fact]
        public async Task ShouldHandleTheMatchingServerHttpAddressNotEndingWithSlash() {
            SetRemoteUrl("http://example.com/foo/bar");

            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
            "http://example.com | foo/bar",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Server = new StaticServer("http://example.com", ""),
                        Url = "{{ base }} | {{ repository }}"
                    },
                    null
                )
            );
        }


        [Fact]
        public async Task ShouldHandleTheMatchingServerSshAddressEndingWithSlash() {
            SetRemoteUrl("ssh://git@example.com:foo/bar");

            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com | foo/bar",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Server = new StaticServer("http://example.com", "ssh://git@example.com/"),
                        Url = "{{ base }} | {{ repository }}"
                    },
                    null
                )
            );
        }


        [Fact]
        public async Task ShouldHandleTheMatchingServerSshAddressNotEndingWithSlash() {
            SetRemoteUrl("ssh://git@example.com:foo/bar");

            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com | foo/bar",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Server = new StaticServer("http://example.com", "ssh://git@example.com"),
                        Url = "{{ base }} | {{ repository }}"
                    },
                    null
                )
            );
        }


        [Fact]
        public async Task ShouldHandleTheMatchingServerSshAddressNotEndingWithColon() {
            SetRemoteUrl("ssh://git@example.com:foo/bar");

            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
              "http://example.com | foo/bar",
              await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Server = new StaticServer("http://example.com/", "ssh://git@example.com"),
                        Url = "{{ base }} | {{ repository }}"
                    },
                    null
                )
            );
        }


        [Fact]
        public async Task ShouldHandleTheMatchingServerSshAddressEndingWithColon() {
            SetRemoteUrl("ssh://git@example.com:foo/bar");

            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com | foo/bar",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Server = new StaticServer("http://example.com/", "ssh://git@example.com:"),
                        Url = "{{ base }} | {{ repository }}"
                    },
                    null
                )
            );
        }


        [Fact]
        public async Task ShouldTrimDotGitFromTheEndOfTheRepositoryPath() {
            SetRemoteUrl("http://example.com/foo/bar.git");

            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com | foo/bar",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Server = new StaticServer("http://example.com", ""),
                        Url = "{{ base }} | {{ repository }}"
                    },
                    null
                )
            );
        }


        [Fact]
        public async Task ShouldHandleSshUrlWithProtocol() {
            SetRemoteUrl("git@example.com:foo/bar");

            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Server = new StaticServer("http://example.com/", "ssh://git@example.com"),
                        Url = "{{ base }}"
                    },
                    null
                )
            );
        }


        [Fact]
        public async Task ShouldHandleSshUrlWithoutProtocol() {
            SetRemoteUrl("git@example.com:foo/bar");

            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Server = new StaticServer("http://example.com/", "git@example.com"),
                        Url = "{{ base }}"
                    },
                    null
                )
            );
        }


        [Fact]
        public async Task ShouldHandleSshWithGitAt() {
            SetRemoteUrl("git@example.com:foo/bar");

            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Server = new StaticServer("http://example.com/", "git@example.com"),
                        Url = "{{ base }}"
                    },
                    null
                )
            );
        }


        [Fact]
        public async Task ShouldHandleSshWithoutGitAt() {
            SetRemoteUrl("git@example.com:foo/bar");

            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Server = new StaticServer("http://example.com/", "example.com"),
                        Url = "{{ base }}"
                    },
                    null
                )
            );
        }


        [Fact]
        public async Task ShouldUseTheRealPathForFilesUnderDirectoryThatIsSymbolicLink() {
            string real;
            string link;


            await SetupRepositoryAsync(RootDirectory);

            real = CreateDirectory("real");
            link = Path.Combine(RootDirectory, "link");

            if (!NativeMethods.CreateSymbolicLink(link, real, NativeMethods.SYMBOLIC_LINK_FLAG_DIRECTORY)) {
                throw new InvalidOperationException("Could not create symlink.");
            }

            CreateFile("real/foo.js");

            Assert.Equal(
                "http://example.com/real/foo.js",
                await CreateUrlAsync(
                    new PartialHandlerDefinition { Url = "{{ base }}/{{ file }}" },
                    LinkType.CurrentBranch,
                    filePath: Path.Combine(link, "foo.js")
                )
            );
        }


        [Fact]
        public async Task ShouldUseTheRealPathForFileThatIsSymbolicLink() {
            string link;
            string file;


            await SetupRepositoryAsync(RootDirectory);

            CreateDirectory("real");
            file = CreateFile("real/foo.js");

            link = Path.Combine(RootDirectory, "link.js");

            if (!NativeMethods.CreateSymbolicLink(link, file, 0)) {
                throw new InvalidOperationException("Could not create symlink.");
            }

            Assert.Equal(
                "http://example.com/real/foo.js",
                await CreateUrlAsync(
                    new PartialHandlerDefinition { Url = "{{ base }}/{{ file }}" },
                    LinkType.CurrentBranch,
                    filePath: link
                )
            );
        }


        [Fact]
        public async Task ShouldNotUseTheRealPathWhenTheEntireGitRepositoryIsUnderSymbolicLink() {
            string real;
            string link;


            real = CreateDirectory("repo");
            await SetupRepositoryAsync(real);

            link = Path.Combine(RootDirectory, "link");

            if (!NativeMethods.CreateSymbolicLink(link, real, NativeMethods.SYMBOLIC_LINK_FLAG_DIRECTORY)) {
                throw new InvalidOperationException("Could not create symlink.");
            }

            SetRepositoryRoot(link);

            CreateFile("repo/foo.js");

            Assert.Equal(
                "http://example.com/foo.js",
                await CreateUrlAsync(
                    new PartialHandlerDefinition { Url = "{{ base }}/{{ file }}" },
                    LinkType.CurrentBranch,
                    filePath: Path.Combine(link, "foo.js")
                )
            );
        }


        [Fact]
        public async Task ShouldNotApplyQueryModificationsWhenNoQueryModificationsMatch() {
            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com/file",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Url = "http://example.com/file",
                        Query = new[] { new QueryModification(new Regex("\\.js$"), "a", "1") }
                    },
                    null,
                    filePath: "foo/bar.txt"
                )
            );
        }


        [Fact]
        public async Task ShouldAddQueryStringIfOneDoesNotExistWhenQueryModificationMatches() {
            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com/file?first=yes",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Url = "http://example.com/file",
                        Query = new[] { new QueryModification(new Regex("\\.txt$"), "first", "yes") }
                    },
                    null,
                    filePath: "foo/bar.txt"
                )
            );
        }


        [Fact]
        public async Task ShouldAddToTheExistingQueryStringIfOneExistsWheQueryModificationMatches() {
            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com/file?first=yes&second=no",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Url = "http://example.com/file?first=yes",
                        Query = new[] { new QueryModification(new Regex("\\.txt$"), "second", "no") }
                    },
                    null,
                    filePath: "foo/bar.txt"
                )
            );
        }


        [Fact]
        public async Task ShouldAddTheQueryStringBeforeTheHashWhenQueryModificationMatches() {
            await SetupRepositoryAsync(RootDirectory);

            Assert.Equal(
                "http://example.com/file?first=yes#L1-10",
                await CreateUrlAsync(
                    new PartialHandlerDefinition {
                        Url = "http://example.com/file#L1-10",
                        Query = new[] { new QueryModification(new Regex("\\.txt$"), "first", "yes") }
                    },
                    null,
                    filePath: "foo/bar.txt"
                )
            );
        }


        private void SetRepositoryRoot(string repo) {
            _repository = new Repository(repo, _repository.Remote);
        }


        private void SetRemoteUrl(string url) {
            _repository = new Repository(_repository.Root, new Remote("origin", url));
        }


        private async Task<string> CreateUrlAsync(PartialHandlerDefinition definition, LinkType? linkType, string filePath = "file.txt") {
            return await CreateHandler(definition).CreateUrlAsync(
                _repository,
                new FileInfo(filePath, null),
                new LinkOptions(linkType)
            );
        }


        private LinkHandler CreateHandler(PartialHandlerDefinition definition) {
            return new LinkHandler(
                new PublicHandlerDefinition(
                    "Test",
                    definition.BranchRef ?? BranchRefType.Abbreviated,
                    Array.Empty<string>(),
                    Template.Parse(definition.Url ?? ""),
                    definition.Query ?? Array.Empty<QueryModification>(),
                    EmptyTemplate,
                    new ReverseSettings(
                        new Regex(""),
                        EmptyTemplate,
                        false,
                        new ReverseServerSettings(EmptyTemplate, EmptyTemplate),
                        new ReverseSelectionSettings(EmptyTemplate, null, null, null)
                    ),
                    new[] { definition.Server ?? new StaticServer("http://example.com", "ssh://example.com") }
                ),
                _settings.Object,
                Git
            );
        }


        private class PartialHandlerDefinition {

            public string? Url { get; set; }


            public StaticServer? Server { get; set; }


            public BranchRefType? BranchRef { get; set; }


            public IReadOnlyList<QueryModification>? Query { get; set; }

        }

    }


    public class GetUrlInfoAsyncMethod : RepositoryTestBase {

        static GetUrlInfoAsyncMethod() {
            TemplateEngine.Initialize();
        }


        [Fact]
        public async Task ShouldReturnNullInStrictModeWhenTheUrlDoesNotMatchTheServer() {
            Assert.Null(
                await GetUrlInfoAsync(
                    new PartialReverseSettings { Pattern = ".+" },
                    "http://different.com/foo/bar.txt",
                    true
                )
            );
        }


        [Fact]
        public async Task ShouldReturnNullWhenThePatternDoesNotMatchTheUrlInStrictMode() {
            Assert.Null(
                await GetUrlInfoAsync(
                    new PartialReverseSettings { Pattern = "^https://.+" },
                    "http://example.com/foo/bar.txt",
                    true
                )
            );
        }


        [Fact]
        public async Task ShouldReturnNullWhenThePatternDoesNotMatchTheUrlInNonStrictMode() {
            Assert.Null(
              await GetUrlInfoAsync(
                    new PartialReverseSettings { Pattern = "^https://.+" },
                    "http://example.com/foo/bar.txt",
                    false
                )
            );
        }


        [Fact]
        public async Task ShouldReturnTheInfoWhenThePatternMatchesTheUrl() {
            Assert.Equal(
                new UrlInfo(
                    "bar.txt",
                    new StaticServer("http", "ssh"),
                    new PartialSelectedRange(10, 20, 30, 40)),
                await GetUrlInfoAsync(
                    new PartialReverseSettings {
                        Pattern = "http://example\\.com/[^/]+/(?<file>.+)",
                        File = "{{ match.groups.file }}",
                        Server = new ReverseServerSettings(
                            Template.Parse("http"),
                            Template.Parse("ssh")
                        ),
                        Selection = new ReverseSelectionSettings(
                            Template.Parse("10"),
                            Template.Parse("20"),
                            Template.Parse("30"),
                            Template.Parse("40")
                        )
                    },
                    "http://example.com/foo/bar.txt",
                    false
                ),
                UrlInfoComparer.Instance
            );
        }


        [Fact]
        public async Task ShouldHandleInvalidSelectionProperties() {
            Assert.Equal(
                new UrlInfo(
                    "bar.txt",
                    new StaticServer("http", "ssh"),
                    new PartialSelectedRange(10, null, null, null)
                ),
                await GetUrlInfoAsync(
                    new PartialReverseSettings {
                        Pattern = "http://example\\.com/[^/]+/(?<file>.+)",
                        File = "{{ match.groups.file }}",
                        Server = new ReverseServerSettings(
                        Template.Parse("http"),
                        Template.Parse("ssh")
                    ),
                        Selection = new ReverseSelectionSettings(
                        Template.Parse("10"),
                        Template.Parse("x"),
                        Template.Parse(""),
                        null
                    )
                    },
                    "http://example.com/foo/bar.txt",
                    false
                ),
                UrlInfoComparer.Instance
            );
        }


        [Fact]
        public async Task ShouldProvideTheMatchingServerInfoToTheTemplates() {
            Assert.Equal(
                new UrlInfo(
                    "",
                    new StaticServer("http://example.com", "example.com"),
                    new PartialSelectedRange(null, null, null, null)
                ),
                await GetUrlInfoAsync(
                    new PartialReverseSettings {
                        Pattern = "http://example\\.com/.+",
                        Server = new ReverseServerSettings(
                            Template.Parse("{{ http }}"),
                            Template.Parse("{{ ssh }}")
                        )
                    },
                    "http://example.com/foo/bar.txt",
                    false
                ),
                UrlInfoComparer.Instance
            );
        }



        private async Task<UrlInfo?> GetUrlInfoAsync(PartialReverseSettings settings, string url, bool strict) {
            return await CreateHandler(settings).GetUrlInfoAsync(url, strict);
        }


        private LinkHandler CreateHandler(PartialReverseSettings reverse) {
            return new LinkHandler(
                new PublicHandlerDefinition(
                    "Test",
                    BranchRefType.Abbreviated,
                    Array.Empty<string>(),
                    EmptyTemplate,
                    Array.Empty<QueryModification>(),
                    EmptyTemplate,
                    new ReverseSettings(
                        new Regex(reverse.Pattern ?? ""),
                        Template.Parse(reverse.File ?? ""),
                        false,
                        reverse.Server ?? new ReverseServerSettings(EmptyTemplate, EmptyTemplate),
                        reverse.Selection ?? new ReverseSelectionSettings(EmptyTemplate, null, null, null)
                    ),
                    new[] { new StaticServer("http://example.com", "ssh://example.com") }
                ),
                Mock.Of<ISettings>(),
                Git
            );
        }


        private class PartialReverseSettings {

            public string? Pattern { get; set; }


            public string? File { get; set; }


            public ReverseServerSettings? Server { get; set; }


            public ReverseSelectionSettings? Selection { get; set; }

        }


        private class UrlInfoComparer : IEqualityComparer<UrlInfo?> {

            public static UrlInfoComparer Instance { get; } = new();


            public bool Equals(UrlInfo? x, UrlInfo? y) {
                if (x is null) {
                    return y is null;
                }

                if (y is null) {
                    return false;
                }

                return string.Equals(x.Server.Http, y.Server.Http, StringComparison.Ordinal) &&
                    string.Equals(x.Server.Ssh, y.Server.Ssh, StringComparison.Ordinal) &&
                    string.Equals(x.FilePath, y.FilePath, StringComparison.Ordinal) &&
                    Nullable.Equals(x.Selection.StartLine, y.Selection.StartLine) &&
                    Nullable.Equals(x.Selection.StartColumn, y.Selection.StartColumn) &&
                    Nullable.Equals(x.Selection.EndLine, y.Selection.EndLine) &&
                    Nullable.Equals(x.Selection.EndColumn, y.Selection.EndColumn);
            }


            public int GetHashCode(UrlInfo? obj) {
                return 0;
            }

        }

    }

}
