﻿// <copyright file="StrictTokenHandlerShould.cs" company="Okta, Inc">
// Copyright (c) 2018-present Okta, Inc. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Okta.AspNet.Abstractions.Test
{
    public class StrictTokenHandlerShould
    {
        [Fact]
        public void AllowGoodToken()
        {
            var fakeIssuer = "example.okta.com";
            var fakeAudience = "aud://default";
            var fakeClient = "fakeClient";

            var claims = new Claim[]
            {
                new Claim("cid", fakeClient),
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("fakesigningsecret!")),
                SecurityAlgorithms.HmacSha256);

            // Create the JWT and write it to a string
            var jwtContents = new JwtSecurityToken(
                issuer: fakeIssuer,
                audience: fakeAudience,
                claims: claims,
                expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)),
                signingCredentials: credentials);
            var jwt = new JwtSecurityTokenHandler().WriteToken(jwtContents);

            var fakeOktaWebOptions = new OktaWebOptions
            {
                ClientId = fakeClient,
                OktaDomain = fakeIssuer,
            };

            var handler = new StrictTokenHandler(fakeOktaWebOptions);

            var validationParameters = new DefaultTokenValidationParameters(fakeOktaWebOptions, fakeIssuer)
            {
                IssuerSigningKey = credentials.Key,
                ValidAudience = fakeAudience,
            };

            handler.ValidateToken(jwt, validationParameters, out _);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("bad")]
        public void RejectBadToken(string badToken)
        {
            var fakeOktaWebOptions = new OktaWebOptions
            {
                ClientId = "fake",
                OktaDomain = "example.okta.com",
            };
            var fakeIssuer = "example.okta.com";

            var handler = new StrictTokenHandler(fakeOktaWebOptions);

            Action act = () => handler.ValidateToken(
                badToken,
                new DefaultTokenValidationParameters(fakeOktaWebOptions, fakeIssuer),
                out _);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void RejectExpiredToken()
        {
            var fakeIssuer = "example.okta.com";
            var fakeAudience = "aud://default";
            var fakeClient = "fakeClient";

            var claims = new Claim[]
            {
                new Claim("cid", fakeClient),
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("fakesigningsecret!")),
                SecurityAlgorithms.HmacSha256);

            // Create the JWT and write it to a string
            var jwtContents = new JwtSecurityToken(
                issuer: fakeIssuer,
                audience: fakeAudience,
                claims: claims,
                expires: DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(3)), // Default clock skew of 2 minutes
                signingCredentials: credentials);
            var jwt = new JwtSecurityTokenHandler().WriteToken(jwtContents);

            var fakeOktaWebOptions = new OktaWebOptions
            {
                ClientId = fakeClient,
                OktaDomain = fakeIssuer,
            };

            var handler = new StrictTokenHandler(fakeOktaWebOptions);

            var validationParameters = new DefaultTokenValidationParameters(fakeOktaWebOptions, fakeIssuer)
            {
                IssuerSigningKey = credentials.Key,
                ValidAudience = fakeAudience,
            };

            Action act = () => handler.ValidateToken(jwt, validationParameters, out _);

            act.Should().Throw<SecurityTokenExpiredException>();
        }

        [Fact]
        public void RejectUnsignedToken()
        {
            var fakeIssuer = "example.okta.com";
            var fakeAudience = "aud://default";
            var fakeClient = "fakeClient";

            var claims = new Claim[]
            {
                new Claim("cid", fakeClient),
            };

            // Create the JWT and write it to a string
            var jwtContents = new JwtSecurityToken(
                issuer: fakeIssuer,
                audience: fakeAudience,
                claims: claims,
                expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)));
                // No signing credentials!

            var jwt = new JwtSecurityTokenHandler().WriteToken(jwtContents);

            var fakeOktaWebOptions = new OktaWebOptions
            {
                ClientId = fakeClient,
                OktaDomain = fakeIssuer,
            };

            var handler = new StrictTokenHandler(fakeOktaWebOptions);

            var validationParameters = new DefaultTokenValidationParameters(fakeOktaWebOptions, fakeIssuer)
            {
                ValidAudience = fakeAudience,
            };

            Action act = () => handler.ValidateToken(jwt, validationParameters, out _);

            act.Should().Throw<SecurityTokenInvalidSignatureException>();
        }

        [Fact]
        public void RejectWrongIssuer()
        {
            var fakeIssuer = "example.okta.com";
            var fakeAudience = "aud://default";
            var fakeClient = "fakeClient";

            var claims = new Claim[]
            {
                new Claim("cid", fakeClient),
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("fakesigningsecret!")),
                SecurityAlgorithms.HmacSha256);

            // Create the JWT and write it to a string
            var jwtContents = new JwtSecurityToken(
                issuer: "different-issuer",
                audience: fakeAudience,
                claims: claims,
                expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)),
                signingCredentials: credentials);
            var jwt = new JwtSecurityTokenHandler().WriteToken(jwtContents);

            var fakeOktaWebOptions = new OktaWebOptions
            {
                ClientId = fakeClient,
                OktaDomain = fakeIssuer,
            };

            var handler = new StrictTokenHandler(fakeOktaWebOptions);

            var validationParameters = new DefaultTokenValidationParameters(fakeOktaWebOptions, fakeIssuer)
            {
                IssuerSigningKey = credentials.Key,
                ValidAudience = fakeAudience,
            };

            Action act = () => handler.ValidateToken(jwt, validationParameters, out _);

            act.Should().Throw<SecurityTokenInvalidIssuerException>();
        }

        [Fact]
        public void RejectWrongClientId()
        {
            var fakeIssuer = "example.okta.com";
            var fakeAudience = "aud://default";
            var fakeClient = "fakeClient";

            var claims = new Claim[]
            {
                new Claim("cid", "different-client"),
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("fakesigningsecret!")),
                SecurityAlgorithms.HmacSha256);

            // Create the JWT and write it to a string
            var jwtContents = new JwtSecurityToken(
                issuer: fakeIssuer,
                audience: fakeAudience,
                claims: claims,
                expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)),
                signingCredentials: credentials);
            var jwt = new JwtSecurityTokenHandler().WriteToken(jwtContents);

            var fakeOktaWebOptions = new OktaWebOptions
            {
                ClientId = fakeClient,
                OktaDomain = fakeIssuer,
            };

            var handler = new StrictTokenHandler(fakeOktaWebOptions);

            var validationParameters = new DefaultTokenValidationParameters(fakeOktaWebOptions, fakeIssuer)
            {
                IssuerSigningKey = credentials.Key,
                ValidAudience = fakeAudience,
            };

            Action act = () => handler.ValidateToken(jwt, validationParameters, out _);

            act.Should().Throw<SecurityTokenValidationException>();
        }

        [Fact]
        public void RejectWrongAudience()
        {
            var fakeIssuer = "example.okta.com";
            var fakeAudience = "aud://default";
            var fakeClient = "fakeClient";

            var claims = new Claim[]
            {
                new Claim("cid", fakeClient),
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("fakesigningsecret!")),
                SecurityAlgorithms.HmacSha256);

            // Create the JWT and write it to a string
            var jwtContents = new JwtSecurityToken(
                issuer: fakeIssuer,
                audience: "http://myapi",
                claims: claims,
                expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)),
                signingCredentials: credentials);
            var jwt = new JwtSecurityTokenHandler().WriteToken(jwtContents);

            var fakeOktaWebOptions = new OktaWebOptions
            {
                ClientId = fakeClient,
                OktaDomain = fakeIssuer,
            };

            var handler = new StrictTokenHandler(fakeOktaWebOptions);

            var validationParameters = new DefaultTokenValidationParameters(fakeOktaWebOptions, fakeIssuer)
            {
                IssuerSigningKey = credentials.Key,
                ValidAudience = fakeAudience,
            };

            Action act = () => handler.ValidateToken(jwt, validationParameters, out _);

            act.Should().Throw<SecurityTokenInvalidAudienceException>();
        }
    }
}
