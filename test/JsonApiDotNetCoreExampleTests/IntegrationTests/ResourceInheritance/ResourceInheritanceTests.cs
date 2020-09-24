using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public sealed class ResourceInheritanceTests : IClassFixture<IntegrationTestContext<
        TestableStartup<ResourceInheritanceDbContext>, ResourceInheritanceDbContext>>
    {
        private readonly
            IntegrationTestContext<TestableStartup<ResourceInheritanceDbContext>, ResourceInheritanceDbContext>
            _testContext;

        public ResourceInheritanceTests(
            IntegrationTestContext<TestableStartup<ResourceInheritanceDbContext>, ResourceInheritanceDbContext>
                testContext)
        {
            _testContext = testContext;

            _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<LiteraturePerson>();
                await dbContext.ClearTableAsync<FictionBook>();
                await dbContext.ClearTableAsync<NonFictionBook>();
                await dbContext.ClearTableAsync<Cat>();
                await dbContext.ClearTableAsync<Dog>();
                await dbContext.ClearTableAsync<Female>();
                await dbContext.ClearTableAsync<Male>();

                await dbContext.SaveChangesAsync();
            }).Wait();
        }
        
        [Fact]
        public async Task When_including_to_one_relationship_should_be_successful()
        {
            // Arrange
            var person = new Male()
            {
                Pet = new Cat(),
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.AddAsync(person);
                await dbContext.SaveChangesAsync();
            });

            var route = "/people?include=pet";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.SingleData.Relationships["pet"].Should().NotBeNull();
            responseDocument.SingleData.Relationships["pet"].SingleData.Type.Should().Be("cats");
            responseDocument.SingleData.Relationships["pet"].SingleData.Id.Should().Be(person.Pet.StringId);
        }
        
        [Fact]
        public async Task When_including_to_many_relationship_should_be_successful()
        {
            // Arrange
            var person = new Male()
            {
                Parents = new List<Person> { new Male(), new Female() },
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.AddAsync(person);
                await dbContext.SaveChangesAsync();
            });

            var route = "/people?include=parents";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.ManyData.Should().HaveCount(3);
            responseDocument.ManyData[0].Relationships["parents"].Should().NotBeNull();
            responseDocument.ManyData[1].Relationships["parents"].Should().BeNull();
            responseDocument.ManyData[2].Relationships["parents"].Should().BeNull();
            responseDocument.ManyData[0].Relationships["parents"].ManyData.Should().HaveCount(2);
            responseDocument.ManyData[0].Relationships["parents"].ManyData[0].Id.Should().Be(person.Parents[0].StringId);
            responseDocument.ManyData[0].Relationships["parents"].ManyData[0].Type.Should().Be("males");
            responseDocument.ManyData[0].Relationships["parents"].ManyData[1].Id.Should().Be(person.Parents[1].StringId);
            responseDocument.ManyData[0].Relationships["parents"].ManyData[1].Type.Should().Be("females");
        }
        
        [Fact]
        public async Task When_including_many_to_many_relationship_should_be_successful()
        {
            // Arrange
            var person = new Male()
            {
                PersonLiterature = new List<LiteraturePerson>
                {
                    new LiteraturePerson { Literature = new FictionBook() },
                    new LiteraturePerson { Literature = new NonFictionBook() }
                }
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.AddAsync(person);
                await dbContext.SaveChangesAsync();
            });

            var route = "/people?include=favoriteLiterature";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Relationships["favoriteLiterature"].Should().NotBeNull();
            responseDocument.ManyData[0].Relationships["favoriteLiterature"].ManyData.Should().HaveCount(2);
            responseDocument.ManyData[0].Relationships["favoriteLiterature"].ManyData[0].Id.Should().Be(person.PersonLiterature[0].Literature.StringId);
            responseDocument.ManyData[0].Relationships["favoriteLiterature"].ManyData[0].Type.Should().Be("fictionBooks");
            responseDocument.ManyData[0].Relationships["favoriteLiterature"].ManyData[1].Id.Should().Be(person.PersonLiterature[1].Literature.StringId);
            responseDocument.ManyData[0].Relationships["favoriteLiterature"].ManyData[1].Type.Should().Be("nonFictionBooks");
        }

        [Fact]
        public async Task When_creating_resource_with_to_one_relationship_should_be_successful()
        {
            // Arrange
            var cat = new Cat();
            
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.AddAsync(cat);
                await dbContext.SaveChangesAsync();
            });

            var route = "/people";
            var requestBody = new
            {
                data = new
                {
                    type = "males",
                    relationships = new Dictionary<string, object>
                    {
                        {
                            "pet", new
                            {
                                data = new {type = "cats", id = cat.StringId}
                            }
                        }
                    }
                }
            };

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var assertPerson = await dbContext.People
                    .Include(p => p.Pet)
                    .Where(p => p.Id.Equals(int.Parse(responseDocument.SingleData.Id))).FirstAsync();
                
                assertPerson.Pet.GetType().Should().Be(cat.GetType());
            });
        }


        [Fact]
        public async Task When_patching_resource_with_to_one_relationship_through_relationship_link_should_be_successful()
        {
            // Arrange
            var person = new Male();
            var cat = new Cat();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.AddRangeAsync(person, cat);
                await dbContext.SaveChangesAsync();
            });
            
            var route = $"/people/{person.Id}/relationships/pet";

            var requestBody = new
            {
                data = new {type = "cats", id = cat.StringId}
            };

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var assertPerson = await dbContext.People
                    .Include(p => p.Pet)
                    .Where(p => p.Id.Equals(person.Id)).FirstAsync();

                assertPerson.Pet.GetType().Should().Be(cat.GetType());
            });
        }


        [Fact]
        public async Task When_creating_resource_with_to_many_relationship_should_be_successful()
        {
            // Arrange
            var father = new Male();
            var mother = new Female();
            
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.AddRangeAsync(father, mother);
                await dbContext.SaveChangesAsync();
            });

            var route = "/people";
            var requestBody = new
            {
                data = new
                {
                    type = "males",
                    relationships = new Dictionary<string, object>
                    {
                        {
                            "parents", new
                            {
                                data = new[]
                                {
                                    new { type = "males", id = father.StringId },
                                    new { type = "females", id = mother.StringId }
                                }
                            }
                        }
                    }
                }
            };

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var assertPerson = await dbContext.People
                    .Include(p => p.Parents)
                    .Where(p => p.Id.Equals(int.Parse(responseDocument.SingleData.Id))).FirstAsync();

                assertPerson.Parents.Should().HaveCount(2);
                assertPerson.Parents.Should().ContainSingle(p => p is Male);
                assertPerson.Parents.Should().ContainSingle(p => p is Female);
            });
        }

        [Fact]
        public async Task When_patching_resource_with_to_many_relationship_through_relationship_link_should_be_successful()
        {
            // Arrange   
            var child = new Male();
            var father = new Male();
            var mother = new Female();
            
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.AddRangeAsync(child, father, mother);
                await dbContext.SaveChangesAsync();
            });
        
            var route = $"/people/{child.StringId}/relationships/parents";
            var requestBody = new
            {
                data = new[]
                {
                    new { type = "males", id = father.StringId },
                    new { type = "females", id = mother.StringId }
                }
            };
        
            // Act
            var (httpResponse, _) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);
        
            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var assertChild = await dbContext.Males
                    .Include(p => p.Parents)
                    .Where(p => p.Id.Equals(child.Id)).FirstAsync();
        
                assertChild.Parents.Should().HaveCount(2);
                assertChild.Parents.Should().ContainSingle(p => p is Male);
                assertChild.Parents.Should().ContainSingle(p => p is Female);
            });
        }

        [Fact]
        public async Task When_creating_resource_with_many_to_many_relationship_should_be_successful()
        {
            // Arrange
            var fiction = new FictionBook();
            var nonFiction = new NonFictionBook();
            
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.AddRangeAsync(fiction, nonFiction);
                await dbContext.SaveChangesAsync();
            });
        
            var route = "/males?include=favoriteLiterature";
            var requestBody = new
            {
                data = new
                {
                    type = "males",
                    relationships = new Dictionary<string, object>
                    {
                        {
                            "favoriteLiterature", new
                            {
                                data = new[]
                                {
                                    new { type = "fictionBooks", id = fiction.StringId },
                                    new { type = "nonFictionBooks", id = nonFiction.StringId }
                                }
                            }
                        }
                    }
                }
            };
        
            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);
        
            // Assert
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var favoriteLiterature = (await dbContext.People
                        .Include(p => p.PersonLiterature)
                        .ThenInclude(pp => pp.Literature)
                        .Where(p => p.Id.Equals(int.Parse(responseDocument.SingleData.Id))).FirstAsync())
                    .PersonLiterature.Select(pp => pp.Literature).ToList();
                
                favoriteLiterature.Should().HaveCount(2);
                favoriteLiterature.Should().ContainSingle(p => p is FictionBook);
                favoriteLiterature.Should().ContainSingle(p => p is NonFictionBook);
            });
        }

        [Fact]
        public async Task When_patching_resource_with_many_to_many_relationship_through_relationship_link_should_be_successful()
        {
            // Arrange
            var fiction = new FictionBook();
            var nonFiction = new NonFictionBook();
            var person = new Male();
            
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.AddRangeAsync(fiction, nonFiction, person);
                await dbContext.SaveChangesAsync();
            });
        
            var route = $"/people/{person.Id}/relationships/favoriteLiterature";
            var requestBody = new
            {
                data = new[]
                {
                    new { type = "fictionBooks", id = fiction.StringId },
                    new { type = "nonFictionBooks", id = nonFiction.StringId }
                }
            };
        
            // Act
            var (httpResponse, _) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);
        
            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var favoriteLiterature = (await dbContext.Males
                        .Include(p => p.PersonLiterature)
                        .ThenInclude(pp => pp.Literature)
                        .Where(p => p.Id.Equals(person.Id)).FirstAsync())
                    .PersonLiterature.Select(pp => pp.Literature).ToList();
        
                favoriteLiterature.Should().HaveCount(2);
                favoriteLiterature.Should().ContainSingle(p => p is FictionBook);
                favoriteLiterature.Should().ContainSingle(p => p is NonFictionBook);
            });
        }
    }
}