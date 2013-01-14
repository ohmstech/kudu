﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class GitRepositoryManagementTests
    {
        [Fact]
        public void PushRepoWithMultipleProjectsShouldDeploy()
        {
            // Arrange
            string appName = KuduUtils.GetRandomWebsiteName("PushMultiProjects");
            string verificationText = "Welcome to ASP.NET MVC!";
            using (var repo = Git.Clone("Mvc3AppWithTestProject"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.NotNull(results[0].LastSuccessEndTime);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact(Skip = "Dangerous")]
        public void PushSimpleWapWithInlineCommand()
        {
            // Arrange
            string appName = KuduUtils.GetRandomWebsiteName("PushSimpleWapWithInlineCommand");

            using (var repo = Git.Clone("CustomBuildScript"))
            {
                repo.WriteFile(".deployment", @"
[config]
command = msbuild SimpleWebApplication/SimpleWebApplication.csproj /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""%DEPLOYMENT_TARGET%"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Debug;SolutionDir=""%DEPLOYMENT_SOURCE%""");
                Git.Commit(repo.PhysicalPath, "Custom build command added");

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "DEBUG");
                });
            }
        }

        [Fact(Skip = "Dangerous")]
        public void PushSimpleWapWithCustomDeploymentScript()
        {
            // Arrange
            string appName = KuduUtils.GetRandomWebsiteName("WapWithCustomDeploymentScript");

            using (var repo = Git.Clone("CustomBuildScript"))
            {
                repo.WriteFile(".deployment", @"
[config]
command = deploy.cmd");
                Git.Commit(repo.PhysicalPath, "Custom build script added");

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "DEBUG");
                });
            }
        }


        [Fact]
        public void PushSimpleWapWithFailingCustomDeploymentScript()
        {
            // Arrange
            string appName = KuduUtils.GetRandomWebsiteName("WapWithFailingCustomDeploymentScript");

            using (var repo = Git.Clone("CustomBuildScript"))
            {
                repo.WriteFile(".deployment", @"
[config]
command = deploy.cmd");
                repo.WriteFile("deploy.cmd", "bogus");
                Git.Commit(repo.PhysicalPath, "Updated the deploy file.");

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, ">bogus");
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "'bogus' is not recognized as an internal or external command");
                });
            }
        }

        [Fact]
        public void WarningsAsErrors()
        {
            // Arrange
            string appName = KuduUtils.GetRandomWebsiteName("WarningsAsErrors");

            using (var repo = Git.Clone("WarningsAsErrors"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    GitDeploymentResult deployResult = appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    Assert.Null(results[0].LastSuccessEndTime);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "Warning as Error: The variable 'x' is declared but never used");
                    Assert.True(deployResult.GitTrace.Contains("Warning as Error: The variable 'x' is declared but never used"));
                    Assert.True(deployResult.GitTrace.Contains("Error - Changes committed to remote repository but your website not updated."));
                });
            }
        }

        [Fact]
        public void PushRepoWithProjectAndNoSolutionShouldDeploy()
        {
            // Arrange
            string appName = KuduUtils.GetRandomWebsiteName("PushRepoWithProjNoSln");
            string verificationText = "Kudu Deployment Testing: Mvc3Application_NoSolution";

            using (var repo = Git.Clone("Mvc3Application_NoSolution"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void PushRepositoryWithNoDeployableProjectsTreatsAsWebsite()
        {
            // Arrange
            string appName = KuduUtils.GetRandomWebsiteName("PushNoDeployableProj");

            using (var repo = Git.Clone("NoDeployableProjects"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "no deployable projects. Deploying files instead");
                });
            }
        }

        [Fact]
        public void WapBuildsReleaseMode()
        {
            // Arrange
            string appName = KuduUtils.GetRandomWebsiteName("WapBuildsRelease");

            using (var repo = Git.Clone("ConditionalCompilation"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, null, "RELEASE MODE", "Context.IsDebuggingEnabled: False");
                });
            }
        }

        [Fact]
        public void WebsiteWithIISExpressWorks()
        {
            // Arrange
            string appName = KuduUtils.GetRandomWebsiteName("WebsiteWithIISExpressWorks");

            using (var repo = Git.Clone("waws"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Home");
                });
            }
        }

        [Fact]
        public void PushAppChangesShouldTriggerBuild()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("PushAppChanges");
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    Git.Revert(repo.PhysicalPath);
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        // This test was only interesting when we used signalR (routing issue), which we don't anymore
        //[Fact]
        public void AppNameWithSignalRWorks()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("signalroverflow");
            string verificationText = "Welcome to ASP.NET MVC!";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, verificationText);
                });
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForWaps()
        {
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("DeletesToRepoForWaps");

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string deletePath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Controllers\AccountController.cs");
                    string projectPath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Mvc3Application.csproj");

                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Account/LogOn", statusCode: HttpStatusCode.OK);

                    File.Delete(deletePath);
                    File.WriteAllText(projectPath, File.ReadAllText(projectPath).Replace(@"<Compile Include=""Controllers\AccountController.cs"" />", ""));
                    Git.Commit(repo.PhysicalPath, "Deleted the filez");
                    appManager.GitDeploy(repo.PhysicalPath);
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Account/LogOn", statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void PushShouldOverwriteModifiedFilesInRepo()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("PushOverwriteModified");
            string verificationText = "The base color for this template is #5c87b2";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string path = @"Content/Site.css";
                    string url = appManager.SiteUrl + path;

                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);

                    KuduAssert.VerifyUrl(url, verificationText);

                    appManager.VfsWebRootManager.WriteAllText(path, "Hello world!");

                    // Sleep a little since it's a remote call
                    Thread.Sleep(500);

                    KuduAssert.VerifyUrl(url, "Hello world!");

                    // Make an unrelated change (newline to the end of web.config)
                    repo.AppendFile(@"Mvc3Application\Web.config", "\n");

                    Git.Commit(repo.PhysicalPath, "This is a test");

                    appManager.GitDeploy(repo.PhysicalPath);

                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(url, verificationText);
                });
            }
        }

        [Fact]
        public void GoingBackInTimeShouldOverwriteModifiedFilesInRepo()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("GoBackOverwriteModified");
            string verificationText = "The base color for this template is #5c87b2";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                string id = repo.CurrentId;

                ApplicationManager.Run(appName, appManager =>
                {
                    string url = appManager.SiteUrl + "/Content/Site.css";
                    // Act
                    appManager.GitDeploy(repositoryName);

                    KuduAssert.VerifyUrl(url, verificationText);

                    repo.AppendFile(@"Mvc3Application\Content\Site.css", "Say Whattttt!");

                    // Make a small changes and commit them to the local repo
                    Git.Commit(repositoryName, "This is a small changes");

                    // Push those changes
                    appManager.GitDeploy(repositoryName);

                    // Make a server site change and verify it shows up
                    appManager.VfsWebRootManager.WriteAllText("Content/Site.css", "Hello world!");

                    Thread.Sleep(500);

                    KuduAssert.VerifyUrl(url, "Hello world!");

                    // Now go back in time
                    appManager.DeploymentManager.DeployAsync(id).Wait();

                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(url, verificationText);
                });
            }
        }

        [Fact]
        public void DeletesToRepositoryArePropagatedForNonWaps()
        {
            string appName = KuduUtils.GetRandomWebsiteName("DeleteForNonWaps");
            using (var repo = Git.Clone("Bakery"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string deletePath = Path.Combine(repo.PhysicalPath, @"Content");

                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);

                    // Assert
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Content/Site.css", statusCode: HttpStatusCode.OK);


                    Directory.Delete(deletePath, recursive: true);
                    Git.Commit(repo.PhysicalPath, "Deleted all styles");

                    appManager.GitDeploy(repo.PhysicalPath);
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "Content/Site.css", statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void FirstPushDeletesPriorContent()
        {
            string appName = KuduUtils.GetRandomWebsiteName("FirstPushDelPrior");
            using (var repo = Git.Clone("HelloWorld"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    appManager.VfsWebRootManager.WriteAllText("foo.txt", "This is a test file");
                    string url = appManager.SiteUrl + "foo.txt";

                    Thread.Sleep(200);

                    KuduAssert.VerifyUrl(url, "This is a test file");

                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);

                    // Assert
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl);
                    KuduAssert.VerifyUrl(url, statusCode: HttpStatusCode.NotFound);
                });
            }
        }

        [Fact]
        public void PushingToNonMasterBranchNoOps()
        {
            string repositoryName = "PushingToNonMasterBranchNoOps";
            string appName = KuduUtils.GetRandomWebsiteName("PushToNonMaster");
            string cloneUrl = "https://github.com/KuduApps/RepoWithMultipleBranches.git";
            using (var repo = Git.Clone(repositoryName, cloneUrl, noCache: true))
            {
                Git.CheckOut(repo.PhysicalPath, "test");
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath, "test", "test");
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(0, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, KuduAssert.DefaultPageContent);
                });
            }
        }

        [Fact]
        public void PushingConfiguredBranch()
        {
            string repositoryName = "PushingConfiguredBranch";
            string appName = KuduUtils.GetRandomWebsiteName("PushingConfiguredBranch");
            string cloneUrl = "https://github.com/KuduApps/RepoWithMultipleBranches.git";
            using (var repo = Git.Clone(repositoryName, cloneUrl, noCache: true))
            {
                Git.CheckOut(repo.PhysicalPath, "test");
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.SettingsManager.SetValue("branch", "test").Wait();
                    appManager.GitDeploy(repo.PhysicalPath, "test", "test");
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Test branch");
                });
            }
        }

        [Fact]
        public void PushingNonMasterBranchToMasterBranchShouldDeploy()
        {
            string repositoryName = "PushingNonMasterBranchToMasterBranchShouldDeploy";
            string appName = KuduUtils.GetRandomWebsiteName("PushNonMasterToMaster");
            string cloneUrl = "https://github.com/KuduApps/RepoWithMultipleBranches.git";
            using (var repo = Git.Clone(repositoryName, cloneUrl, noCache: true))
            {
                Git.CheckOut(repo.PhysicalPath, "test");
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath, "test", "master");
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Test branch");
                });
            }
        }

        [Fact]
        public void CloneFromEmptyRepoAndPushShouldDeploy()
        {
            string repositoryName = "CloneFromEmptyRepoAndPushShouldDeploy";
            string appName = KuduUtils.GetRandomWebsiteName("CloneEmptyAndPush");

            ApplicationManager.Run(appName, appManager =>
            {
                // Act
                using (var repo = Git.Clone(repositoryName, appManager.GitUrl))
                {
                    // Add a file
                    repo.WriteFile("hello.txt", "Wow");
                    Git.Commit(repo.PhysicalPath, "Added hello.txt");
                    string helloUrl = appManager.SiteUrl + "/hello.txt";

                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(helloUrl, "Wow");
                }
            });
        }

        [Fact]
        public void PushThenClone()
        {
            string repositoryName = "Bakery";
            string appName = KuduUtils.GetRandomWebsiteName("PushThenClone");

            using (var repo = Git.Clone(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);

                    using (var clonedRepo = Git.Clone(repositoryName, appManager.GitUrl))
                    {
                        Assert.True(clonedRepo.FileExists("Default.cshtml"));
                    }
                });
            }
        }

        [Fact]
        public void CloneFromNewRepoShouldHaveBeEmpty()
        {
            string repositoryName = "CloneFromNewRepoShouldHaveFile";
            string appName = KuduUtils.GetRandomWebsiteName("CloneNew");

            ApplicationManager.Run(appName, appManager =>
            {
                using (var repo = Git.Clone(repositoryName, appManager.GitUrl))
                {
                    Assert.False(repo.FileExists("index.html"));
                }
            });
        }

        [Fact]
        public void ClonesInParallel()
        {
            string appName = KuduUtils.GetRandomWebsiteName("ClonesInParallel");

            ApplicationManager.Run(appName, appManager =>
            {
                var t1 = Task.Factory.StartNew(() => DoClone("PClone1", appManager));
                var t2 = Task.Factory.StartNew(() => DoClone("PClone2", appManager));
                var t3 = Task.Factory.StartNew(() => DoClone("PClone3", appManager));
                Task.WaitAll(t1, t2, t3);
            });
        }

        private static void DoClone(string repositoryName, ApplicationManager appManager)
        {
            using (var repo = Git.Clone(repositoryName, appManager.GitUrl))
            {
                Assert.False(repo.FileExists("index.html"));
            }
        }

        [Fact]
        public void GoingBackInTimeDeploysOldFiles()
        {
            string appName = KuduUtils.GetRandomWebsiteName("GoBackDeployOld");
            using (var repo = Git.Clone("HelloWorld"))
            {
                string originalCommitId = repo.CurrentId;

                ApplicationManager.Run(appName, appManager =>
                {
                    // Deploy the app
                    appManager.GitDeploy(repo.PhysicalPath);

                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    KuduAssert.VerifyUrl(appManager.SiteUrl);

                    // Add a file
                    File.WriteAllText(Path.Combine(repo.PhysicalPath, "hello.txt"), "Wow");
                    Git.Commit(repo.PhysicalPath, "Added hello.txt");
                    string helloUrl = appManager.SiteUrl + "/hello.txt";

                    // Deploy those changes
                    appManager.GitDeploy(repo.PhysicalPath);

                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(helloUrl, "Wow");

                    // Go back to the first deployment
                    appManager.DeploymentManager.DeployAsync(originalCommitId).Wait();

                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    KuduAssert.VerifyUrl(helloUrl, statusCode: HttpStatusCode.NotFound);
                    Assert.Equal(2, results.Count);
                });
            }
        }

        [Fact]
        public void NpmSiteInstallsPackages()
        {
            string appName = KuduUtils.GetRandomWebsiteName("NpmInstallsPkg");

            using (var repo = Git.Clone("NpmSite"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(appManager.VfsWebRootManager.Exists("node_modules/express"));
                });
            }
        }

        [Fact]
        public void FailedNpmFailsDeployment()
        {
            string appName = KuduUtils.GetRandomWebsiteName("FailedNpm");

            using (var repo = Git.Clone("NpmSite"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Replace the express dependency with something that doesn't exist
                    repo.Replace("package.json", "express", "MadeUpKuduPackage");
                    Git.Commit(repo.PhysicalPath, "Added fake package to package.json");
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    KuduAssert.VerifyLogOutput(appManager, results[0].Id, "'MadeUpKuduPackage' is not in the npm registry.");
                });
            }
        }

        [Fact(Skip = "Not a valid scenario")]
        public void CustomNodeScript()
        {
            string repositoryName = "CustomNodeScript";
            string appName = KuduUtils.GetRandomWebsiteName("CustomNodeScript");
            string webConfig = @"
<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <system.webServer>         
      <handlers>
           <add name=""iisnode"" path=""server.js"" verb=""*"" modules=""iisnode""/>
     </handlers>
      <rewrite>
           <rules>
                <rule name=""StaticContent"">
                     <action type=""Rewrite"" url=""public{REQUEST_URI}""/>
                </rule>
                <rule name=""DynamicContent"">
                     <conditions>
                          <add input=""{REQUEST_FILENAME}"" matchType=""IsFile"" negate=""True""/>
                     </conditions>
                     <action type=""Rewrite"" url=""server.js""/>
                </rule>
           </rules>
      </rewrite>
    <iisnode 
      nodeProcessCommandLine=""&quot;%programfiles(x86)%\nodejs\node.exe&quot;""
      debuggingEnabled=""false""
      logDirectory=""..\..\LogFiles\nodejs"" 
      watchedFiles=""*.js;iisnode.yml;node_modules\*;views\*.jade;views\*.ejb;routes\*.js"" />
   </system.webServer>
 </configuration>";

            var path = Git.GetRepositoryPath(repositoryName);

            using (var repo = Git.Init(path))
            {
                repo.WriteFile("build.js", String.Format(@"var fs = require('fs');
console.log('Creating server.js on the fly!');
console.log('target is ' + process.env.DEPLOYMENT_TARGET);
fs.writeFileSync(process.env.DEPLOYMENT_TARGET + '\server.js', ""var http = require('http'); http.createServer(function (req, res) {{ res.writeHead(200, {{'Content-Type': 'text/html'}}); res.end('Hello, world! [helloworld sample; iisnode version is ' + process.env.IISNODE_VERSION + ', node version is ' + process.version + ']'); }}).listen(process.env.PORT);"");
console.log('Done!');", webConfig));
                repo.WriteFile(".deployment", @"
[config]
command = node build.js
");
                repo.WriteFile("web.config", webConfig);

                Git.Commit(repo.PhysicalPath, "Added build.js");

                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl);
                });
            }
        }

        [Fact]
        public void NodeHelloWorldNoConfig()
        {
            string appName = KuduUtils.GetRandomWebsiteName("NodeConfig");

            using (var repo = Git.Clone("NodeHelloWorldNoConfig"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);

                    // Make sure a web.config file got created at deployment time
                    Assert.True(appManager.VfsWebRootManager.Exists("web.config"));
                });
            }
        }

        [Fact]
        public void RedeployNodeSite()
        {
            string appName = KuduUtils.GetRandomWebsiteName("RedeployNodeSite");

            using (var repo = Git.Clone("NodeHelloWorldNoConfig"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);

                    string id = results[0].Id;

                    repo.Replace("server.js", "world", "world2");
                    Git.Commit(repo.PhysicalPath, "Made a small change");

                    appManager.GitDeploy(repo.PhysicalPath);
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[1].Status);

                    appManager.DeploymentManager.DeployAsync(id).Wait();
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                });
            }
        }

        [Fact]
        public void GetResultsWithMaxItemsAndExcludeFailed()
        {
            // Arrange
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("RsltMaxItemXcldFailed");

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    string homeControllerPath = Path.Combine(repo.PhysicalPath, @"Mvc3Application\Controllers\HomeController.cs");

                    // Act, Initial Commit
                    appManager.GitDeploy(repo.PhysicalPath);

                    // Assert
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET MVC! - Change1");

                    // Act, Change 2
                    File.WriteAllText(homeControllerPath, File.ReadAllText(homeControllerPath).Replace(" - Change1", " - Change2"));
                    Git.Commit(repo.PhysicalPath, "Change 2!");
                    appManager.GitDeploy(repo.PhysicalPath);

                    // Assert
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET MVC! - Change2");

                    // Act, Invalid Change build-break change and commit
                    File.WriteAllText(homeControllerPath, File.ReadAllText(homeControllerPath).Replace("Index()", "Index;"));
                    Git.Commit(repo.PhysicalPath, "Invalid Change!");
                    appManager.GitDeploy(repo.PhysicalPath);

                    // Assert
                    results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                    Assert.Equal(3, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    Assert.Equal(DeployStatus.Success, results[1].Status);
                    Assert.Equal(DeployStatus.Success, results[2].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, "Welcome to ASP.NET MVC! - Change2");

                    // Test maxItems = 2
                    results = appManager.DeploymentManager.GetResultsAsync(maxItems: 2).Result.ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Failed, results[0].Status);
                    Assert.Equal(DeployStatus.Success, results[1].Status);

                    // Test excludeFailed = true
                    results = appManager.DeploymentManager.GetResultsAsync(excludeFailed: true).Result.ToList();
                    Assert.Equal(2, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.Equal(DeployStatus.Success, results[1].Status);

                    // Test maxItems = 1, excludeFailed = true
                    results = appManager.DeploymentManager.GetResultsAsync(maxItems: 1, excludeFailed: true).Result.ToList();
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    Assert.True(results[0].Current);
                });
            }
        }

        [Fact]
        public void RepoWithPublicSubModuleTest()
        {
            // Arrange
            string repositoryName = "RepoWithPublicSubModule";
            string appName = KuduUtils.GetRandomWebsiteName("RepoWithPublicSubModule");
            string cloneUrl = "https://github.com/KuduApps/RepoWithPublicSubModule.git";

            using (var repo = Git.Clone(repositoryName, cloneUrl, noCache: true))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "default.htm", "Hello from RepoWithPublicSubModule!");
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "PublicSubModule/default.htm", "Hello from PublicSubModule!");
                });
            }
        }

        [Fact]
        public void RepoWithPrivateSubModuleTest()
        {
            // Arrange
            string appName = KuduUtils.GetRandomWebsiteName("RepoWithPrivateSubModule");
            string id_rsa;

            IDictionary<string, string> environmentVariables = SshHelper.PrepareSSHEnv(out id_rsa);
            if (String.IsNullOrEmpty(id_rsa))
            {
                return;
            }

            using (var repo = Git.Clone("RepoWithPrivateSubModule", environments: environmentVariables))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    appManager.SSHKeyManager.SetPrivateKey(id_rsa);

                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(DeployStatus.Success, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "default.htm", "Hello from RepoWithPrivateSubModule!");
                    KuduAssert.VerifyUrl(appManager.SiteUrl + "PrivateSubModule/default.htm", "Hello from PrivateSubModule!");
                });
            }
        }

        [Fact]
        public void HangProcessTest()
        {
            // Arrange
            string appName = KuduUtils.GetRandomWebsiteName("HangProcess");

            using (var repo = Git.Clone("HangProcess"))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // Act
                    // Set IdleTimeout to 10s meaning there must be activity every 10s
                    // Otherwise process and its child will be terminated
                    appManager.SettingsManager.SetValue(SettingsKeys.CommandIdleTimeout, "10").Wait();

                    // This HangProcess repo spew out activity at 2s, 4s, 6s and 30s respectively
                    // we should receive the one < 10s and terminate otherwise.
                    GitDeploymentResult result = appManager.GitDeploy(repo.PhysicalPath);
                    Assert.Contains("remote: Sleep(2000)", result.GitTrace);
                    Assert.Contains("remote: Sleep(4000)", result.GitTrace);
                    Assert.Contains("remote: Sleep(6000)", result.GitTrace);
                    Assert.DoesNotContain("remote: Sleep(30000)", result.GitTrace);
                    Assert.Contains("remote: Process 'starter.cmd' aborted due to idle timeout.", result.GitTrace);
                });
            }
        }

        [Fact]
        public void ScmTypeTest()
        {
            // Arrange
            using (var repo = Git.Clone("HelloKudu"))
            {
                string appName = KuduUtils.GetRandomWebsiteName("ScmTypeTest");
                ApplicationManager.Run(appName, appManager =>
                {
                    // Default value test
                    try
                    {
                        // for private kudu, the setting is LocalGit
                        string value = appManager.SettingsManager.GetValue(SettingsKeys.ScmType).Result;
                        Assert.Equal("LocalGit", value);
                    }
                    catch (AggregateException ex)
                    {
                        // for public kudu, the setting does not exist
                        Assert.Contains("404", ex.InnerExceptions[0].Message);
                    }

                    // Initial deployment
                    GitDeploymentResult result = appManager.GitDeploy(repo.PhysicalPath);
                    Assert.Contains("remote: Deployment successful.", result.GitTrace);

                    // Disable by setting to None
                    appManager.SettingsManager.SetValue(SettingsKeys.ScmType, ScmType.None.ToString()).Wait();
                    KuduAssert.ThrowsMessage("The requested URL returned error: 403", () => appManager.GitDeploy(repo.PhysicalPath));
                    var client = CreateClient(appManager);
                    var post = new Dictionary<string, string>
                    {
                        { "payload", String.Empty }
                    };
                    HttpResponseMessage response = client.PostAsync("deploy", new FormUrlEncodedContent(post)).Result;
                    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

                    // Enable by setting to GitHub
                    appManager.SettingsManager.SetValue(SettingsKeys.ScmType, "GitHub").Wait();
                    result = appManager.GitDeploy(repo.PhysicalPath);
                    Assert.Contains("Everything up-to-date", result.GitTrace);
                    client = CreateClient(appManager);
                    response = client.PostAsync("deploy", new FormUrlEncodedContent(post)).Result;
                    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

                    // Disable by setting to Tfs
                    appManager.SettingsManager.SetValue(SettingsKeys.ScmType, ScmType.Tfs.ToString()).Wait();
                    KuduAssert.ThrowsMessage("The requested URL returned error: 403", () => appManager.GitDeploy(repo.PhysicalPath));
                    client = CreateClient(appManager);
                    response = client.PostAsync("deploy", new FormUrlEncodedContent(post)).Result;
                    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                });
            }
        }

        [Fact]
        public void SpecificDeploymentConfiguration()
        {
            VerifyDeploymentConfiguration("SpecificDeployConfig",
                                          "WebApplication1",
                                          "This is the application I want deployed");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForProjectFile()
        {
            VerifyDeploymentConfiguration("DeployConfigForProj",
                                          "WebApplication1/WebApplication1.csproj",
                                          "This is the application I want deployed");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForWebsite()
        {
            VerifyDeploymentConfiguration("DeployConfigForWeb",
                                          "WebSite1",
                                          "This is a website!");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForWebsiteWithSlash()
        {
            VerifyDeploymentConfiguration("DeployConfigWithSlash",
                                          "/WebSite1",
                                          "This is a website!");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForWebsiteNotPartOfSolution()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForWebsiteNotPartOfSolution",
                                          "WebSiteRemovedFromSolution",
                                          "This web site was removed from solution!");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForWebProjectNotPartOfSolution()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForWebProjectNotPartOfSolution",
                                          "MvcApplicationRemovedFromSolution",
                                          "This web project was removed from solution!");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForNonDeployableProjectFile()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForNonDeployableProjectFile",
                                          "MvcApplicationRemovedFromSolution.Tests/MvcApplicationRemovedFromSolution.Tests.csproj",
                                          KuduAssert.DefaultPageContent,
                                          DeployStatus.Failed);
        }

        [Fact]
        public void SpecificDeploymentConfigurationForNonDeployableProject()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForNonDeployableProject",
                                          "MvcApplicationRemovedFromSolution.Tests",
                                          KuduAssert.DefaultPageContent,
                                          DeployStatus.Failed,
                                          "is not a deployable project");
        }

        [Fact]
        public void SpecificDeploymentConfigurationForDirectoryThatDoesNotExist()
        {
            VerifyDeploymentConfiguration("SpecificDeploymentConfigurationForDirectoryThatDoesNotExist",
                                          "IDoNotExist",
                                          KuduAssert.DefaultPageContent,
                                          DeployStatus.Failed);
        }

        private static HttpClient CreateClient(ApplicationManager appManager)
        {
            HttpClientHandler handler = HttpClientHelper.CreateClientHandler(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(appManager.ServiceUrl),
                Timeout = TimeSpan.FromMinutes(5)
            };
        }

        private void VerifyDeploymentConfiguration(string siteName, string targetProject, string expectedText, DeployStatus expectedStatus = DeployStatus.Success, string expectedLog = null)
        {
            string name = KuduUtils.GetRandomWebsiteName(siteName);
            string cloneUrl = "https://github.com/KuduApps/SpecificDeploymentConfiguration.git";
            using (var repo = Git.Clone(name, cloneUrl))
            {
                ApplicationManager.Run(name, appManager =>
                {
                    string deploymentFile = Path.Combine(repo.PhysicalPath, @".deployment");
                    File.WriteAllText(deploymentFile, String.Format(@"[config]
project = {0}", targetProject));
                    Git.Commit(repo.PhysicalPath, "Updated configuration");

                    // Act
                    appManager.GitDeploy(repo.PhysicalPath);
                    var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

                    // Assert
                    Assert.Equal(1, results.Count);
                    Assert.Equal(expectedStatus, results[0].Status);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, expectedText);
                    if (!String.IsNullOrEmpty(expectedLog))
                    {
                        KuduAssert.VerifyLogOutput(appManager, results[0].Id, expectedLog);
                    }
                });
            }
        }
    }
}
