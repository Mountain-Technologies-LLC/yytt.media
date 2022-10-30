using Amazon.CDK;
using Constructs;

namespace YyttMedia
{
    public class YyttMediaStack : Stack
    {
        /// <summary>
        /// ## This stack uses
        /// - YyttMediaConstruct: a construct to deploy a static website within the distributable folder called `dist`
        ///
        /// ## From terminal from location of the cdk.json file
        /// To synth stack
        /// `cdk synth --context domainName=yytt.media`
        ///
        /// To deploy stack
        /// `cdk deploy --context domainName=yytt.media`
        /// </summary>
        internal YyttMediaStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            new YyttMediaConstruct(this, "yyttMediaConstruct", new YyttMediaConstructProps
            {
                DomainName = (string)this.Node.TryGetContext("domainName")
            });
        }
    }
}
