---
services: active-directory
platforms: dotnet
author: dstrockis
---

# Build a multi-tenant SaaS web application that calls a web API using Azure AD

This sample shows how to build a multi-tenant .Net MVC web application that uses OpenID Connect to sign up and sign in users from any Azure Active Directory (AD) tenant, using the ASP.Net OpenID Connect OWIN middleware and the Active Directory Authentication Library (ADAL) for .NET. The sample also demonstrates how to leverage the authorization code received at sign in time to invoke the Graph API.

For more information about how the protocols work in this scenario and other scenarios, see the [Authentication Scenarios for Azure AD](https://azure.microsoft.com/documentation/articles/active-directory-authentication-scenarios/) document.

## How To Run This Sample

Getting started is simple!  To run this sample you will need:
- Visual Studio 2013
- An Internet connection
- An Azure subscription (a free trial is sufficient)

Every Azure subscription has an associated Azure Active Directory tenant, which provides authentication and authorization services for the subscription. You can easily create new tenants within your subscription for testing purposes. For details on the relationship between an Azure subscription and Azure AD tenant, see [How Azure subscriptions are associated with Azure AD](https://azure.microsoft.com/documentation/articles/active-directory-how-subscriptions-associated-directory/#manage-the-directory-for-your-office-365-subscription-in-azure).

If you don't already have an Azure subscription, you can get a free subscription by signing up at [http://wwww.windowsazure.com](http://www.windowsazure.com). All of the Azure AD features used by this sample are available free of charge. 

### Step 1:  Clone or download this repository

From your shell or command line:

`git clone https://github.com/Azure-Samples/active-directory-dotnet-webapp-webapi-multitenant-openidconnect.git`

### Step 2:  Create an Organizational user account in your Azure Active Directory tenant

If you already have an Organizational user account in your Azure Active Directory tenant that you would like to use for consent and authentication, you can skip to the next step.  This sample will not work with a Microsoft account, so if you signed in to the Azure portal with a Microsoft account and have never created a user account in your directory before, you need to do that now. You can find instructions to do that [here](http://www.cloudidentity.com/blog/2013/12/11/setting-up-an-asp-net-project-with-organizational-authentication-requires-an-organizational-account/). 

If you want to test both the Administrator and User consent flows discussed below, you will want to create two Organizational accounts: one assigned to the "User" role and one assigned to the ["Global Administrator"](https://azure.microsoft.com/documentation/articles/active-directory-assign-admin-roles/) role. If you want to use the Organizational account to sign-in to the Azure portal, don't forget to also add it as a co-administrator of your Azure subscription.

### Step 3:  Register the sample with your Azure Active Directory tenant

1. Sign in to the [Azure management portal](https://manage.windowsazure.com).
2. Click on Active Directory in the left hand nav.
3. Click the directory tenant where you wish to register the sample application.
4. Click the Applications tab.
5. Click the Add option at the bottom of the page.
6. Click "Add an application my organization is developing".
7. Enter a friendly name for the application, for example "TodoListWebApp_MT", select "Web Application and/or Web API", and click the Next arrow on the lower right.
8. For the Sign-on URL, enter the base URL for the sample, which is by default `https://localhost:44302/`.
9. For the App ID URI, enter `https://<your_tenant_domain>/TodoListWebApp_MT`, replacing `<your_tenant_domain>` with the domain of your Azure AD tenant (either in the form `<tenant_name>.onmicrosoft.com` or your own custom domain if you registered it in Azure Active Directory).
10. Click the Complete checkmark on the lower right corner. Your application is now provisioned and you will be returned to it's main application page.
11. Click on the Configure tab of your application.
12. Find "the application is multi-tenant" switch and flip it to "yes", then click the Save option at the bottom of the page.
12. Scroll down to the "Permissions to other applications" section. On the Windows Azure Active Directory row, click on the "Delegated Permissions" combo box and note that the "Sign-on and read users profile" permission is already enabled, which is assigned by default whenever a new application is registered. This will allow our application to receive delegated permission to authenticate and read user profile data, for a given user account. The list of permissions provided here are known as permissions scopes, some of which require Administrator consent. See the [Graph API Permissions Scopes](https://msdn.microsoft.com/Library/Azure/Ad/Graph/api/graph-api-permission-scopes) article for more information.

Don't close the browser yet, as we will still need to work with the portal for few more steps. 

### Step 4:  Provision a key for your app in your Azure Active Directory tenant

The application will need to authenticate with Azure AD in order to participate in the Auth2 flow, which requires you to associate a private key with the application you registered in your tenant. In order to do this:
 
1. Click the Configure tab of your application.
2. Scroll down to the "Keys" section. In the Select Duration dropdown, pick a value (either is fine for the demo).
3. Click the Save option at the bottom of the page.
4. Once the Save operation has completed, the value of the key appears. Copy the key to the clipboard. **Important: this is the only opportunity you have to access the value of the key, if you don't use it now you'll have to create a new one.**

Leave the browser open to this page. 

### Step 5:  Configure the sample to use your Azure Active Directory tenant

At this point we are ready to paste the configuration settings into the VS project that will tie it to its entry in your Azure AD tenant. 

1. Open the solution in Visual Studio 2013, by double clicking on the WebApp-WebAPI-MultiTenant-OpenIDConnect-DotNet.sln file in the repository you closed in Step 1.
2. Open the `web.config` file in the TodoListWebApp project, and locate the <appSettings> section.
3. Find the key `ida:Password` and replace the value in quotes in the `value` attribute with the string you copied in step 4.
4. Go back to the portal, find the CLIENT ID field and copy its content to the clipboard
5. Find the key `ida:ClientID` and replace the value in quotes the `value` attribute with the CLIENT ID from the Azure portal.

### Step 6:  [optional] Create an Azure Active Directory test tenant 

This sample shows how to take advantage of the consent framework in Azure AD to enable an application to be multi-tenant aware, which allows authentication by user accounts from any Azure AD tenant. To see that part of the sample in action, you need to have access to user accounts from a tenant that is different from the one you used for registering the application. A great example of this type of scenario, is an application that needs to allow Office365 user accounts (which are homed in a separate Azure AD) to authenticate and consent access to their Office365 tenant. The simplest way of doing this is to create a new directory tenant in your Azure subscription (just navigate to the main Active Directory page in the portal and click Add) and add test users.
This step is optional as you can also use accounts from the same directory, but if you do you will not see the consent prompts as the app is already approved. 

### Step 5:  Run the sample

The sample implements two distinct tasks: the onboarding of a new customer (aka: Sign up), and regular sign in & use of the application.
####  Sign up
1. Start the application. Click on Sign Up.
2. You will be presented with a form that simulates an onboarding process. Here you can choose whether you want to follow the "admin consent" flow (the app gets provisioned for all the users in one organization - requires you to sign up using an administrator), or the "user consent" flow (the app gets provisioned for your user only).
3. Click the SignUp button. You'll be transferred to the Azure AD portal. Sign in as the user you want to use for consenting. 4. If the user is from a tenant that is different from the one where the app was developed, you will be presented with a consent page. Click OK. You will be transported back to the app, where your registration will be finalized.
####  Sign in
Once you signed up, you can either click on the Todo tab or the sign in link to gain access to the application. Note that if you are doing this in the same session in which you signed up, you will automatically sign in with the same account you used for signing up. If you are signing in during a new session, you will be presented with Azure AD's credentials prompt: sign in using an account compatible with the sign up option you chose earlier (the exact same account if you used user consent, any user form the same tenant if you used admin consent). 

## How To Deploy This Sample to Azure

Coming soon.

## About The Code

The application is subdivided in three main functional areas:

1. Common assets
2. Sign up
3. Todo editor

Let's briefly list the noteworthy elements in each area. For more details please refer to the comments in the code.

### Common assets

The application relies on models defined in Models/AppModels.cs, stored via entities as described by the context and initializer classes in the DAL folder.
The Home controller provides the basis for the main experience, listing all the actions the user can perform and providing conditional UI elements for explicit sign in and sign out (driven by the Account controller).

### Sign Up

The sign up operations are handled by the Onboarding controller.
The SignUp action and corresponding view simulate a simple onboarding experience, which results in an OAuth2 code grant request that triggers the consent flow.
The ProcessCode action receives authorization codes from Azure AD and, if they appear valid (see the code comments for details), it creates entries in the application store for the new customer organization/user.

### Todo editor

This is the application proper.
Its core resource is the Todo controller, a CRUD editor which leverages claims and the entity framework to manage a personalized list of Todo items for the currently signed in user.
The Todo controller is secured via OpenId Connect, according to the logic in App_Start/Startup.Auth.cs.

####Notable code

The following code turns off the default Issuer validation, given that in the multitenant case the list of acceptable issuer values is dynamic and cannot be acquired via metadata (as it is instead the case for the single organization case). 

    TokenValidationParameters = new System.IdentityModel.Tokens.TokenValidationParameters
    {
       ValidateIssuer = false,
    }

The following handler for `RedirectToIdentityProvider` assigns to the `Redirect_Uri` and `Post_Logout_Redirect_Uri` (properties used for sign in and sign out locations) URLs that reflect the current address of the application. This allows you to deploy the app to Azure Web Sites or any other location without having to change hardcoded address settings. Note that you do need to add the intended addresses to the Azure AD entry for your application.

    RedirectToIdentityProvider = (context) =>
    {
       string appBaseUrl = context.Request.Scheme + "://" + context.Request.Host + context.Request.PathBase;
       context.ProtocolMessage.Redirect_Uri = appBaseUrl;
       context.ProtocolMessage.Post_Logout_Redirect_Uri = appBaseUrl;
       return Task.FromResult(0);
    }

Finally: the implementation of `SecurityTokenValidated` contains the custom caller validation logic, comparing the incoming token with the database of trusted tenants and registered users and interrupting the authentication sequence if a match is not found.

All of the OWIN middleware in this project is created as a part of the open source [Katana project](http://katanaproject.codeplex.com).  You can read more about OWIN [here](http://owin.org).
