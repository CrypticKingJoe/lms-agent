﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMS.Users.Profiles
{
    using AutoMapper;
    using Dto;
    using Portal.LicenseMonitoringSystem.Users.Entities;

    public  class LicenseUserProfile : Profile
    {
        public LicenseUserProfile()
        {
            CreateMap<LicenseUserDto, LicenseUser>()
                .ForMember(dest => dest.Groups, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.IsExcluded, opt => opt.Ignore())
                .ForMember(dest => dest.UserType, opt => opt.Ignore());
        }
    }
}
