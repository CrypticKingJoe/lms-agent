﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseMonitoringSystem.Tests.Core.Veeam
{
    using Abp;
    using global::Core.Veeam;
    using Shouldly;
    using Xunit;

    public class VeeamLicenseManager_Tests : LicenseMonitoringSystemTestBase
    {
        private string _exampleLicense = @"﻿<?xml version=""1.0"" encoding=""utf-8""?>
<Licenses><License><![CDATA[CPU sockets (vSphere)=16
Company=Central Technology Ltd
Description=Copyright 2016 Veeam, Inc. All Rights Reserved. The Software Product is protected by copyright and other intellectual property laws and treaties. Veeam or its suppliers own the title, copyright, and other intellectual property rights in the Software Product. Veeam reserves all rights not expressly granted to you in EULA. The Software Product is licensed, not sold. Veeam grants to you a nonexclusive nontransferable license to use the Software Product, provided that you agree with EULA.
E-mail=chris.barr@ct.co.uk
Edition=Enterprise
Expiration date=30/11/2017
First name=Christopher
Last name=Barr
License information=License type:\tRental\nLicensed to:\tCentral Technology Ltd\nContact person:\tChristopher Barr\nLicensed Sockets:\t0\nEdition:       \tEnterprise\nManaged VMs (vSphere):\t378\nManaged VMs (Hyper-V):\t0\nCPU sockets (vSphere):\t16\nManaged VMs (vSphere):\t378
License type=Rental
Managed VMs (Hyper-V)=0
Managed VMs (vSphere)=378
Product=Veeam Backup and Replication
Version=9.x
Installation time=30606421:-779068240]]></License></Licenses>
";

        private readonly VeeamLicenseManager _vlm;
        public VeeamLicenseManager_Tests()
        {
            _vlm = new VeeamLicenseManager(_exampleLicense);
        }

        [Fact]
        public void ExtractPropertiesFromLicense_ShouldReturnDictionary()
        {            
            // act
            var value = _vlm.ExtractPropertiesFromLicense();

            // assert
            value.ShouldNotBeNull();
            value.ShouldBeOfType<Dictionary<string, string>>();
        }

        [Fact]
        public void GetProperty_ShouldThrowException_WhenPropertyDoesNotExist()
        {
            // act && assert
            ShouldThrowExtensions.ShouldThrow<AbpException>(() => _vlm.GetProperty(Guid.NewGuid().ToString()));
        }

        [Fact]
        public void GetProperty_ShouldReturnString_WhenPropertyExists()
        {
            // act
            var sut = _vlm.GetProperty("Edition");

            // assert
            sut.ShouldBeOfType<string>();
            sut.ShouldBe("Enterprise");
        }

        [Fact]
        public void GetPropertyNoThrow_ShouldReturnString_WhenPropertyExists()
        {
            // act
            var sut = _vlm.GetPropertyNoThrow("Expiration date");

            // assert
            sut.ShouldBeOfType<string>();
            sut.ShouldBe("30/11/2017");
        }

        [Fact]
        public void GetPropertyNoThrow_ShouldReturnEmptyString_WhenPropertyDoesNotExist()
        {
            // act
            var sut = _vlm.GetPropertyNoThrow(Guid.NewGuid().ToString());

            // assert
            sut.ShouldBeOfType<string>();
            sut.ShouldBe(string.Empty);
        }
    }
}
