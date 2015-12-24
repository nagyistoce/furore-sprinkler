﻿/* 
 * Copyright (c) 2014, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.github.com/furore-fhir/sprinkler/master/LICENSE
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Furore.Fhir.Sprinkler.Framework.Framework;
using Furore.Fhir.Sprinkler.Framework.Utilities;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace Furore.Fhir.Sprinkler.TestSet
{
    [SprinklerModule("Search")]
    public class SearchTest : SprinklerTestClass
    {
        private Condition newCondition;
        private Patient newPatient;

        [ModuleInitialize]
        public void Initialize()
        {
            newPatient = Utils.GetNewPatient("Leroy");
            newPatient = Client.Create(newPatient);
           newCondition = new Condition
            {
                Patient = new ResourceReference
                {
                    Reference = "Patient/" + newPatient.Id
                }
            };
            newCondition = Client.Create(newCondition);
        }

        [SprinklerTest("SE01", "Search resource type without criteria")]
        public void SearchResourcesWithoutCriteria()
        {
            int pageSize = 10;
            Bundle result = Client.Search<Patient>(pageSize: pageSize);
            BundleAssert.CheckConditionForResources(result, r => r.Id != null || r.VersionId != null,   "Resources must have id/versionId information");
            BundleAssert.CheckMinimumNumberOfElementsInBundle(result, 1);
            BundleAssert.CheckMaximumNumberOfElementsInBundle(result, pageSize);

            if (!result.Entry.ByResourceType<Patient>().Any())
                Assert.Fail("search returned entries other than patient");
        }

        [SprinklerTest("SE02", "Search on non-existing resource")]
        public void TrySearchNonExistingResource()
        {
            Assert.Fails(Client, () => Client.Search("Nonexistingnonpatientresource"), HttpStatusCode.NotFound);
        }

        [SprinklerTest("SE03", "Search patient resource on partial familyname")]
        public void SearchResourcesWithNameCriterium()
        {
            //Assert.SkipWhen(allPatients == null);
            //// First create a search argument: any family name present in the
            //// previous unlimited search result that has at least 5 characters
            //string name = allPatients.Entry.ByResourceType<Patient>()
            //    .Where(p => p.Name != null)
            //    .SelectMany(p => p.Name)
            //    .Where(hn => hn.Family != null)
            //    .SelectMany(hn => hn.Family)
            //    .Where(s => s.Length > 5).First();

            // Take the first three characters
            string name = newPatient.Name.SelectMany(n => n.Family).First().Substring(0, 3);


            Bundle result = Client.Search<Patient>(new[] {"family=" + name});
            Assert.EntryIdsArePresentAndAbsoluteUrls(result);

            BundleAssert.CheckMinimumNumberOfElementsInBundle(result, 1);

            // Each patient returned should have a family name with the
            // criterium
            IEnumerable<string> names = result.Entry.ByResourceType<Patient>()
                .Where(p => p.Name != null)
                .SelectMany(p => p.Name)
                .Where(hn => hn.Family != null)
                .SelectMany(hn => hn.Family);

            bool correct = result.Entry.ByResourceType<Patient>()
                .All(p => p.Name != null &&
                          p.Name.Where(hn => hn.Family != null)
                              .SelectMany(hn => hn.Family)
                              .Any(s => s.ToLower().Contains(name.ToLower())));

            if (!correct)
                Assert.Fail("search result contains patients that do not match the criterium");
        }

        [SprinklerTest("SE04", "Search patient resource on given name")]
        public void SearchPatientOnGiven()
        {
            Patient patient = new Patient();    
            patient.Name = new List<HumanName> { HumanName.ForFamily("Adams").WithGiven("Fester") };
            Client.Create(patient);
            Bundle bundle = Client.Search<Patient>(new[] {"given=Fester"});        

            var correctName = from pat in bundle.Entry.ByResourceType<Patient>()
                              where pat.Name.Any(hn=> hn.Family.Contains("Adams") && hn.GivenElement.Any(e=> e.Value == "Fester"))//HumanName.ForFamily("Adams").WithGiven("Fester"))
                              select pat;

            bool found = correctName.Count() > 0; 

            Assert.IsTrue(found, "Patient was not found with given name");
        }

        [SprinklerTest("SE05", "Search condition by subject (patient) reference - given as url")]
        public void SearchConditionByPatientReference()
        {
            var patientRef = new ResourceIdentity(newCondition.Patient.Url);

            Bundle result = Client.Search<Condition>(new[] { "patient=" + patientRef });
            Assert.EntryIdsArePresentAndAbsoluteUrls(result);

            Assert.CorrectNumberOfResults(1, result.Entry.Count(),
                "conditions for this patient (using patient=)");

        }

        [SprinklerTest("SE32", "Search condition by subject (patient) reference - given just as id")]
        public void SearchEncounterByPatientReference()
        {
            Bundle result = Client.Search<Condition>(new[] { "patient=" + newPatient.Id });
            Assert.EntryIdsArePresentAndAbsoluteUrls(result);

            Assert.CorrectNumberOfResults(1, result.Entry.Count(),
                "conditions for this patient (using patient=)");

        }

        [SprinklerTest("SE06", "Search with includes")]
        public void SearchWithIncludes()
        {
            Bundle bundle = Client.Search<Condition>(new[] { "_id=" + newCondition.Id, "_include=Condition:patient" });

            IEnumerable<Patient> patients = bundle.Entry.ByResourceType<Patient>();
            Assert.IsTrue(patients.Count() > 0,
                "Search Conditions with _include=Condition.subject should have patients");
        }

        private string CreateObservation(decimal value)
        {
            var observation = new Observation();
            observation.Status = Observation.ObservationStatus.Preliminary;
            observation.Code = new CodeableConcept() { Coding = new List<Coding>() { new Coding("http://loinc.org", "2164-2"), new Coding("http://snomed.org", "abc123") }, Text = "Code text" };
            observation.Value = new Quantity
            {
                System = "http://unitsofmeasure.org",
                Value = value,
                Code = "mg",
                Unit = "miligram"
            };
            observation.BodySite = new CodeableConcept("http://snomed.info/sct", "182756003");

            Observation entry = Client.Create(observation);
            return entry.Id;
        }

        [SprinklerTest("SE21", "Search for quantity (in observation) - precision tests")]
        public void SearchQuantity()
        {
            string id0 = CreateObservation(4.12345M);
            string id1 = CreateObservation(4.12346M);
            string id2 = CreateObservation(4.12349M);

            Bundle bundle = Client.Search("Observation", new[] {"value-quantity=4.1234||mg"});

            Assert.IsTrue(bundle.ContainsResource(id0), "Search on quantity value 4.1234 should return 4.12345");
            Assert.IsTrue(!bundle.ContainsResource(id1), "Search on quantity value 4.1234 should not return 4.12346");
            Assert.IsTrue(!bundle.ContainsResource(id2), "Search on quantity value 4.1234 should not return 4.12349");

            
        }

        [SprinklerTest("SE22", "Search for quantity (in observation) - operators")]
        public void SearchQuantityGreater()
        {
            string id0 = CreateObservation(4.12M);
            string id1 = CreateObservation(5.12M);
            string id2 = CreateObservation(6.12M);

            Bundle bundle = Client.Search("Observation", new[] {"value-quantity=gt5||mg"});

            BundleAssert.CheckMinimumNumberOfElementsInBundle(bundle, 2);

            BundleAssert.CheckTypeForResources<Observation>(bundle);
            BundleAssert.CheckConditionForResources<Observation>(bundle, o => o.Value is Quantity && ((Quantity)o.Value).Value > 5, "All observation should have a quantity larger than 5 mg");
            Assert.IsTrue(!bundle.ContainsResource(id0), "Search greater than quantity should not return lesser value.");
        }


        [SprinklerTest("SE30", "Search for decimal parameters with trailing spaces")]
        public void SearchObservationQuantityWith0Decimal()
        {
            string id1 = CreateObservation(6M);
            string id2 = CreateObservation(6.0M);

            Bundle bundle = Client.Search("Observation", new[] { "value-quantity=6||mg" });
            BundleAssert.CheckContainedResources<Observation>(bundle, new string[]{id1, id2});

            bundle = Client.Search("Observation", new[] { "value-quantity=6.0||mg" });
            BundleAssert.CheckContainedResources<Observation>(bundle, new string[] { id2 });

            Client.Delete("Observation/"+id1);
            Client.Delete("Observation/" + id2);
        }

        [SprinklerTest("SE23", "Search with quantifier :missing, on Patient.gender.")]
        public void SearchPatientByGenderMissing()
        {
            var patients = new List<Patient>();
            var filter = "family=BROOKS";
            Bundle bundle = Client.Search<Patient>(new[] { filter });
            while (bundle != null && bundle.Entry.ByResourceType<Patient>().Count() > 0)
            {
                patients.AddRange(bundle.Entry.ByResourceType<Patient>());
                bundle = Client.Continue(bundle);
            }
            IEnumerable<Patient> patientsNoGender = patients.Where(p => p.Gender == null);
            int nrOfPatientsWithMissingGender = patientsNoGender.Count();
            IEnumerable<Patient> actual =
                Client.Search<Patient>(new[] { "gender:missing=true", filter }, pageSize: 500).Entry.ByResourceType<Patient>();

            Assert.CorrectNumberOfResults(nrOfPatientsWithMissingGender, actual.Count(),
                "Expected {0} patients without gender, but got {1}.");
        }

        [SprinklerTest("SE24", "Search with non-existing parameter.")]
        public void SearchPatientByNonExistingParameter()
        {
            int nrOfAllPatients = Client.Search<Patient>().Entry.ByResourceType<Patient>().Count();
            Bundle actual = Client.Search("Patient", new[] {"noparam=nonsense"});
                //Obviously a non-existing search parameter
            int nrOfActualPatients = actual.Entry.ByResourceType<Patient>().Count();
            Assert.CorrectNumberOfResults(nrOfAllPatients, nrOfActualPatients,
                "Expected all patients ({0}) since the only search parameter is non-existing, but got {1}.");
            IEnumerable<OperationOutcome> outcomes = actual.Entry.ByResourceType<OperationOutcome>();
        }

        [SprinklerTest("SE25", "Search with malformed parameter.")]
        public void SearchPatientByMalformedParameter()
        {
            Bundle actual = null;
            Assert.Fails(Client, () => Client.Search("Patient", new[] {"...=test"}), out actual,
                HttpStatusCode.BadRequest);
        }

        [SprinklerTest("SE26", "Search for deleted resources.")]
        public void SearchShouldNotReturnDeletedResource()
        {
            Patient patientToDelete = Utils.GetNewPatient("Leroy");
            patientToDelete = Client.Create(patientToDelete);
            Client.Delete(patientToDelete);

            Bundle bundle = Client.Search<Patient>(new[] { "_id=" + patientToDelete.Id });
            BundleAssert.CheckBundleEmpty(bundle);
        }

        [SprinklerTest("SE27", "Paging forward and backward through a search result")]
        public void PageThroughResourceSearch()
        {
            DateTimeOffset searchStartDate = DateTimeOffset.Now;
            Patient patient1 = Utils.GetNewPatient("PageThroughResourceSearch");
            patient1 = Client.Create(patient1);
            Patient patient2 = Utils.GetNewPatient("PageThroughResourceSearch");
            patient2 = Client.Create(patient2);

            int pageSize = 1;
            Bundle page = Client.Search("Patient", new string[]
            {
                "family=PageThroughResourceSearch"
            }, null, pageSize);

            int forwardCount = TestBundlePages(page, PageDirection.Next, pageSize);
            int backwardsCount = TestBundlePages(Client.Continue(page, PageDirection.Last), PageDirection.Previous,
                pageSize);

            if (forwardCount != backwardsCount)
            {
                Assert.Fail(String.Format("Paging forward returns {0} entries, backwards returned {1}",
                    forwardCount, backwardsCount));
            }

            Client.Delete(patient1);
            Client.Delete(patient2);
        }

        [SprinklerTest("SE28", "Search for code (in observation) - token parameter")]
        public void SearchWithToken()
        {
            string id0 = CreateObservation(4.12345M);

            Bundle bundle = Client.Search("Observation", new[] { "code=http://loinc.org/|2164-2" });

            Assert.IsTrue(bundle.ContainsResource(id0), "Search on code with system 'http://loinc.org/' and code '2164-2' should return observation");

            bundle = Client.Search("Observation", new[] { "code=2164-2" });

            Assert.IsTrue(bundle.ContainsResource(id0), "Search on code with *no* system and code '2164-2' should return observation");

            bundle = Client.Search("Observation", new[] { "code=|2164-2" });

            Assert.IsTrue(!bundle.ContainsResource(id0), "Search on code with system '<empty>' and code '2164-2' should not return observation");

            bundle = Client.Search("Observation", new[] { "code=completelyBonkersNamespace|2164-2" });

            Assert.IsTrue(!bundle.ContainsResource(id0), "Search on code with system 'completelyBonkersNamespace' and code '2164-2' should return nothing");

            Client.Delete("Observation/" + id0);
        }

        [SprinklerTest("SE31", "Using type query string parameter")]
        public void SearchUsingTypeSearchParameter()
        {
            Location loc = new Location();
            loc.Type = new CodeableConcept("http://hl7.org/fhir/v3/RoleCode", "RNEU", "test type");
            loc = Client.Create(loc);

            Bundle bundle = Client.Search("Location", new string[]
            {
                "type=RNEU"
            });

            BundleAssert.CheckTypeForResources<Location>(bundle);
            BundleAssert.CheckMinimumNumberOfElementsInBundle(bundle, 1);

            Client.Delete(loc);

        }

        private int TestBundlePages(Bundle page, PageDirection direction, int pageSize)
        {
            int pageCount = 0;
            while (page != null)
            {
                pageCount++;
                BundleAssert.CheckConditionForResources(page, r => r.Id != null || r.VersionId != null,
                    "Resources must have id/versionId information");
                BundleAssert.CheckMaximumNumberOfElementsInBundle(page, pageSize);

                page = Client.Continue(page, direction);
            }
            return pageCount;
        }
    }
}