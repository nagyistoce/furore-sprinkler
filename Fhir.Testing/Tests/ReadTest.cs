﻿/* 
 * Copyright (c) 2014, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.github.com/furore-fhir/sprinkler/master/LICENSE
 */

using System.Collections.Generic;
using System.Net;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Sprinkler.Framework;

namespace Sprinkler.Tests
{
    [SprinklerTestModule("Read")]
    public class ReadTest : SprinklerTestClass
    {
        public static Patient NewPatient(string family, params string[] given)
        {
            var p = new Patient();
            var n = new HumanName();
            foreach (string g in given)
            {
                n.WithGiven(g);
            }

            n.AndFamily(family);
            p.Name = new List<HumanName>();
            p.Name.Add(n);
            return p;
        }

        [SprinklerTest("R001", "Result headers on normal read")]
        public void GetTestDataPerson()
        {
            Patient p = NewPatient("Emerald", "Caro");
            ResourceEntry<Patient> entry = Client.Create(p, null, false);
            string id = entry.GetBasicId();

            ResourceEntry<Patient> pat = Client.Read<Patient>(id);

            HttpTests.AssertHttpOk(Client);

            HttpTests.AssertValidResourceContentTypePresent(Client);
            HttpTests.AssertLastModifiedPresent(Client);
            HttpTests.AssertContentLocationPresentAndValid(Client);
        }

        [SprinklerTest("R002", "Read unknown resource type")]
        public void TryReadUnknownResourceType()
        {
            ResourceIdentity id = ResourceIdentity.Build(Client.Endpoint, "thisreallywondexist", "1");
            HttpTests.AssertFail(Client, () => Client.Read<Patient>(id), HttpStatusCode.NotFound);

            // todo: if the Content-Type header was not set by the server, this generates an abstract exception:
            // "The given key was not present in the dictionary";
        }

        [SprinklerTest("R003", "Read non-existing resource id")]
        public void TryReadNonExistingResource()
        {
            HttpTests.AssertFail(Client, () => Client.Read<Patient>("Patient/3141592unlikely"), HttpStatusCode.NotFound);
        }

        [SprinklerTest("R004", "Read bad formatted resource id")]
        public void TryReadBadFormattedResourceId()
        {
            //Test for Spark issue #7, https://github.com/furore-fhir/spark/issues/7
            HttpTests.AssertFail(Client, () => Client.Read<Patient>("Patient/ID-may-not-contain-CAPITALS"),
                HttpStatusCode.BadRequest);
        }
    }
}