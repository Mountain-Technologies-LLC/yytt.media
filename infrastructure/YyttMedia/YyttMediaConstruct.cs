/* MIT License

Copyright (c) 2022 Mountain Technologies LLC

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. */

using Amazon.CDK;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.Route53.Targets;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Constructs;

namespace YyttMedia
{
    public class YyttMediaConstructProps
    {
        public string DomainName;
    }

    public class YyttMediaConstruct : Construct
    {
        internal YyttMediaConstruct(Construct scope, string id, YyttMediaConstructProps props) : base(scope, id)
        {
            var domainName = props.DomainName;
            var wwwDomainName = $"www.{domainName}";
            var scopedAws = new ScopedAws(this);

            var hostedZone = HostedZone.FromLookup(this, "hostedZone", new HostedZoneProviderProps { DomainName = domainName });

            var dnsValidatedCertificate = new DnsValidatedCertificate(
                this, "dnsValidatedCertificate",
                new DnsValidatedCertificateProps
                {
                    DomainName = domainName,
                    SubjectAlternativeNames = new[] { wwwDomainName },
                    HostedZone = hostedZone,
                    Region = scopedAws.Region,
                    Validation = CertificateValidation.FromDns()
                });

            var bucketName = $"{domainName}-{scopedAws.Region}-{scopedAws.AccountId}";

            var s3Bucket = new Bucket(
                this, "s3Bucket",
                new BucketProps
                {
                    BucketName = bucketName,
                    BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                    RemovalPolicy = RemovalPolicy.DESTROY,
                    AutoDeleteObjects = true
                });

            var cloudFrontOriginAccessIdentity = new OriginAccessIdentity(
                this, "cloudFrontOriginAccessIdentity",
                new OriginAccessIdentityProps { Comment = "Yytt.Media" });

            var cloudFrontDistribution = new Distribution(
                this, "distribution",
                new DistributionProps
                {
                    DomainNames = new[] { domainName, wwwDomainName },
                    DefaultBehavior = new BehaviorOptions
                    {
                        Origin = new S3Origin(s3Bucket, new S3OriginProps { OriginAccessIdentity = cloudFrontOriginAccessIdentity }),
                        Compress = true,
                        AllowedMethods = AllowedMethods.ALLOW_GET_HEAD,
                        CachedMethods = CachedMethods.CACHE_GET_HEAD,
                        ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                        CachePolicy = CachePolicy.CACHING_OPTIMIZED
                    },
                    ErrorResponses = new[]
                    {
                        new ErrorResponse { HttpStatus = 403, ResponsePagePath = "/index.html", ResponseHttpStatus = 403, Ttl = Duration.Minutes(0) },
                        new ErrorResponse { HttpStatus = 404, ResponsePagePath = "/index.html", ResponseHttpStatus = 404, Ttl = Duration.Minutes(0) }
                    },
                    PriceClass = PriceClass.PRICE_CLASS_100, // USA, Canada, Europe, & Israel
                    Enabled = true,
                    Certificate = dnsValidatedCertificate,
                    MinimumProtocolVersion = SecurityPolicyProtocol.TLS_V1_2_2021,
                    HttpVersion = HttpVersion.HTTP2,
                    DefaultRootObject = "index.html",
                    EnableIpv6 = true
                });

            var route53ARecord = new ARecord(
                this, "route53ARecord",
                new ARecordProps
                {
                    RecordName = domainName,
                    Zone = hostedZone,
                    Target = RecordTarget.FromAlias(new CloudFrontTarget(cloudFrontDistribution))
                });

            new ARecord(
                this, "wwwRoute53ARecord",
                new ARecordProps
                {
                    RecordName = wwwDomainName,
                    Zone = hostedZone,
                    Target = RecordTarget.FromAlias(new Route53RecordTarget(route53ARecord))
                });

            new BucketDeployment(
                this, "s3BucketDeploy",
                new BucketDeploymentProps
                {
                    Sources = new[] { Source.Asset("./dist") },
                    DestinationBucket = s3Bucket,
                    Distribution = cloudFrontDistribution,
                    DistributionPaths = new[] { "/*" }
                });

            new CfnOutput(this, "DeployUrl", new CfnOutputProps { Value = $"https://{domainName}" });
        }
    }
}
