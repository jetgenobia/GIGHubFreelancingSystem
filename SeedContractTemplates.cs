using Freelancing.Data;
using Freelancing.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Freelancing
{
    public static class SeedContractTemplates
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Check if templates already exist
            if (await context.ContractTemplates.AnyAsync())
            {
                return;
            }

            var templates = new List<ContractTemplate>
            {
                new ContractTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = "Standard Freelance Agreement",
                    Description = "A comprehensive freelance contract suitable for most projects",
                    Category = "General",
                    IsActive = true,
                    TemplateVersion = "1.0",
                    TemplateContent = GetStandardTemplate()
                },
                new ContractTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = "Websites, IT & Software Contract",
                    Description = "Specialized contract for web, mobile, and other IT related projects",
                    Category = "Websites, IT & Software",
                    IsActive = true,
                    TemplateVersion = "1.0",
                    TemplateContent = GetITTemplate()
                },
                new ContractTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = "Design & Media Contract",
                    Description = "Contract template for graphic design and creative work",
                    Category = "Design & Media",
                    IsActive = true,
                    TemplateVersion = "1.0",
                    TemplateContent = GetDesignMediaTemplate()
                },
                new ContractTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = "Writing & Content Contract",
                    Description = "Contract template for writing and content creation projects",
                    Category = "Writing",
                    IsActive = true,
                    TemplateVersion = "1.0",
                    TemplateContent = GetWritingTemplate()
                }
            };

            context.ContractTemplates.AddRange(templates);
            await context.SaveChangesAsync();
        }

        private static string GetStandardTemplate()
        {
            return @"
               <div class='contract-header mb-6'>
            <h1 class=""text-center font-bold mb-2"">FREELANCE SERVICE AGREEMENT</h1>
            <p>This Freelance Service Agreement (""Agreement"") is entered into on <strong>{{CONTRACT_DATE}}</strong> by and between:</p>
        </div>

        <div class='parties-section mb-6'>
            <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                <h2 class=""text-center text-white font-bold"">PARTIES</h2>
            </div>

            <!-- mobile-first: stacked on small screens, side-by-side on md+ -->
            <div class='parties-container flex flex-col gap-5 md:flex-row md:gap-8'>
                <div class='party-info w-full md:w-1/2'>
                    <h3 class = ""italic"">Client:</h3>
                    <p><strong>Name:</strong> {{CLIENT_NAME}}</p>
                    <p><strong>Email:</strong> {{CLIENT_EMAIL}}</p>
                </div>

                <div class='party-info w-full md:w-1/2'>
                    <h3 class = ""italic"">Freelancer:</h3>
                    <p><strong>Name:</strong> {{FREELANCER_NAME}}</p>
                    <p><strong>Email:</strong> {{FREELANCER_EMAIL}}</p>
                </div>
            </div>
        </div>

                <div class='project-section mb-6'>
                    <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                        <h2 class=""text-center text-white font-bold"">PROJECT DETAILS</h2>
                    </div>

                    <p><strong>Project Name:</strong> {{PROJECT_NAME}}</p>
                    <p><strong>Category:</strong> {{PROJECT_CATEGORY}}</p>
                    <p><strong>Description:</strong></p>
                    <div class='project-description'>{{PROJECT_DESCRIPTION}}</div>
                </div>

                <div class='terms-section mb-6'>
                    <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                        <h2 class=""text-center text-white font-bold"">TERMS AND CONDITIONS</h2>
                    </div>
                    
                    <h3 class = ""mb-1 italic"">1. Payment Terms</h3>
                    <p><strong>Total Project Value:</strong> {{AGREED_AMOUNT}}</p>
                    {{PAYMENT_TERMS_SECTION}}
                    
                    <h3 class = ""mb-1 mt-4 italic"">2. Project Timeline</h3>
                    {{PROJECT_TIMELINE_SECTION}}
                    <p class = ""mb-3"">The Freelancer agrees to complete the project within the specified timeline, subject to timely feedback and approvals from the Client.</p>
                    
                    <h3 class = ""mb-1 italic"">3. Revision Policy</h3>
                    {{REVISION_POLICY_SECTION}}
                    
                    <h3 class = ""mb-1 mt-4 italic"">4. Deliverables</h3>
                    <p class = ""mb-4"">The Freelancer will deliver the agreed-upon work product as specified in the project description and proposal.</p>
                    
                    <h3 class = ""mb-1 italic"">5. Intellectual Property</h3>
                    <p class = ""mb-4"">Upon full payment, all intellectual property rights for the delivered work will transfer to the Client, except for any pre-existing intellectual property of the Freelancer.</p>
                    
                    <h3 class = ""mb-1 italic"">6. Confidentiality</h3>
                    <p class = ""mb-4"">Both parties agree to maintain confidentiality regarding any proprietary information shared during the course of this project.</p>
                    
                    <h3 class = ""mb-1 italic"">7. Termination</h3>
                    <p class = ""mb-4"">Either party may terminate this agreement with written notice. In case of termination, payment made during the start of contract up to the termination date is finalized.</p>
                    
                    <h3 class = ""mb-1 italic"">8. Dispute Resolution</h3>
                    <p>Any disputes arising from this agreement will be resolved through good faith negotiation. If necessary, disputes will be resolved through binding arbitration.</p>
                </div>

                <div class='agreement-section mb-6'>
                    <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                        <h2 class=""text-center text-white font-bold"">AGREEMENT</h2>
                    </div>
                    <p>By signing below, both parties agree to the terms and conditions set forth in this agreement.</p>
                    
                    <p><strong>This contract becomes effective upon signatures from both parties.</strong></p>
                </div>
            ";
        }

        private static string GetITTemplate()
        {
            return @"
                <div class='contract-header mb-6'>
            <h1 class=""text-center font-bold mb-2"">WEBSITES, IT & SOFTWARE DEVELOPMENT SERVICE AGREEMENT</h1>
            <p>This Websites, IT & Software Service Agreement (""Agreement"") is entered into on <strong>{{CONTRACT_DATE}}</strong> by and between:</p>
        </div>

        <div class='parties-section mb-6'>
            <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                <h2 class=""text-center text-white font-bold"">PARTIES</h2>
            </div>

            <div class='parties-container flex flex-col gap-5 md:flex-row md:gap-8'>
                <div class='party-info w-full md:w-1/2'>
                    <h3 class = ""italic"">Client:</h3>
                    <p><strong>Name:</strong> {{CLIENT_NAME}}</p>
                    <p><strong>Email:</strong> {{CLIENT_EMAIL}}</p>
                </div>

                <div class='party-info w-full md:w-1/2'>
                    <h3 class = ""italic"">Freelancer:</h3>
                    <p><strong>Name:</strong> {{FREELANCER_NAME}}</p>
                    <p><strong>Email:</strong> {{FREELANCER_EMAIL}}</p>
                </div>
            </div>
        </div>

                <div class='project-section mb-6'>
                    <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                        <h2 class=""text-center text-white font-bold"">PROJECT DETAILS</h2>
                    </div>

                    <p><strong>Project Name:</strong> {{PROJECT_NAME}}</p>
                    <p><strong>Description:</strong></p>
                    <div class='project-description'>{{PROJECT_DESCRIPTION}}</div>
                </div>

                <div class='terms-section mb-6'>
                    <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                        <h2 class=""text-center text-white font-bold"">TERMS AND CONDITIONS</h2>
                    </div>
                    
                    <h3 class = ""mb-1 italic"">1. Payment Terms</h3>
                    <p><strong>Total Development Cost:</strong> {{AGREED_AMOUNT}}</p>
                    {{PAYMENT_TERMS_SECTION}}
                    
                    <h3 class = ""mb-1 mt-4 italic"">2. Development Timeline</h3>
                    {{PROJECT_TIMELINE_SECTION}}
                    <p>Timeline includes development, testing, and deployment phases.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">3. Revision Policy</h3>
                    {{REVISION_POLICY_SECTION}}
                    
                    <h3 class = ""mb-1 mt-4 italic"">4. Technical Deliverables</h3>
                    <ul>
                        <li>Fully functional website/application/software/other IT related projects</li>
                        <li>Source code and documentation</li>
                        <li>Testing and quality assurance</li>
                        <li>Deployment and launch support</li>
                    </ul>
                    
                    <h3 class = ""mb-1 mt-4 italic"">5. Browser and OS Compatibility</h3>
                    <p>The website/application/software/other IT related projects will be tested and compatible with major modern browsers and operating system.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">6. Responsive Design</h3>
                    <p>The website/application/software/other IT related projects will be responsive and optimized for desktop, tablet, and mobile devices.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">7. Content Management</h3>
                    <p>If applicable, the Developer will provide training for content management system usage.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">8. Hosting and Domain</h3>
                    <p>Client is responsible for hosting and domain registration unless otherwise specified.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">9. Maintenance Period</h3>
                    <p>A 30-day warranty period is included for bug fixes and minor adjustments after project completion.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">10. Code Ownership</h3>
                    <p>Upon full payment, the Client will own all custom code developed for this project.</p>
                </div>

                <div class='agreement-section mb-6'>
                    <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                        <h2 class=""text-center text-white font-bold"">AGREEMENT</h2>
                    </div>
                    <p>By signing below, both parties agree to the terms and conditions set forth in this web development agreement.</p>
                </div>
            ";
        }

        private static string GetDesignMediaTemplate()
        {
            return @"
                <div class='contract-header mb-6'>
            <h1 class=""text-center font-bold mb-2"">DESIGN & MEDIA SERVICE AGREEMENT</h1>
            <p>This Design & Media Service Agreement (""Agreement"") is entered into on <strong>{{CONTRACT_DATE}}</strong> by and between:</p>
        </div>

        <div class='parties-section mb-6'>
            <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                <h2 class=""text-center text-white font-bold"">PARTIES</h2>
            </div>

            <div class='parties-container flex flex-col gap-5 md:flex-row md:gap-8'>
                <div class='party-info w-full md:w-1/2'>
                    <h3 class = ""italic"">Client:</h3>
                    <p><strong>Name:</strong> {{CLIENT_NAME}}</p>
                    <p><strong>Email:</strong> {{CLIENT_EMAIL}}</p>
                </div>

                <div class='party-info w-full md:w-1/2'>
                    <h3 class = ""italic"">Freelancer:</h3>
                    <p><strong>Name:</strong> {{FREELANCER_NAME}}</p>
                    <p><strong>Email:</strong> {{FREELANCER_EMAIL}}</p>
                </div>
            </div>
        </div>

                <div class='project-section mb-6'>
                    <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                        <h2 class=""text-center text-white font-bold"">PROJECT DETAILS</h2>
                    </div>

                    <p><strong>Project Name:</strong> {{PROJECT_NAME}}</p>
                    <p><strong>Description:</strong></p>
                    <div class='project-description'>{{PROJECT_DESCRIPTION}}</div>
                </div>

                <div class='terms-section mb-6'>
                    <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                        <h2 class=""text-center text-white font-bold"">TERMS AND CONDITIONS</h2>
                    </div>
                    
                    <h3 class = ""mb-1 italic"">1. Design Fee</h3>
                    <p><strong>Total Design Fee:</strong> {{AGREED_AMOUNT}}</p>
                    {{PAYMENT_TERMS_SECTION}}
                    
                    <h3 class = ""mb-1 mt-4 italic"">2. Project Timeline</h3>
                    {{PROJECT_TIMELINE_SECTION}}
                    
                    <h3 class = ""mb-1 mt-4 italic"">3. Revision Policy</h3>
                    {{REVISION_POLICY_SECTION}}
                    
                    <h3  class = ""mb-1 mt-4 italic"">4. Design Deliverables</h3>
                    <p>Final deliverables will include high-resolution files in agreed formats (AI, PSD, PNG, JPG, PDF, etc.).</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">5. Copyright and Usage Rights</h3>
                    <p>Upon full payment, the Client receives full rights to use the final designs for their intended purpose.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">6. Stock Images and Fonts</h3>
                    <p>Client is responsible for licensing any stock images or premium fonts required for the project.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">7. Brand Guidelines</h3>
                    <p>If applicable, the Designer will provide brand guidelines and usage instructions.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">8. Print Specifications</h3>
                    <p>For print projects, final files will be provided in print-ready format with appropriate color profiles.</p>
                </div>

                <div class='agreement-section mb-6'>
                    <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                        <h2 class=""text-center text-white font-bold"">AGREEMENT</h2>
                    </div>
                    <p>By signing below, both parties agree to the terms and conditions set forth in this graphic design agreement.</p>
                </div>
            ";
        }

        private static string GetWritingTemplate()
        {
            return @"
               <div class='contract-header mb-6'>
            <h1 class=""text-center font-bold mb-2"">WRITING & CONTENT CREATION AGREEMENT</h1>
            <p>This Writing & Content Creation Agreement (""Agreement"") is entered into on <strong>{{CONTRACT_DATE}}</strong> by and between:</p>
        </div>

        <div class='parties-section mb-6'>
            <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                <h2 class=""text-center text-white font-bold"">PARTIES</h2>
            </div>

            <div class='parties-container flex flex-col gap-5 md:flex-row md:gap-8'>
                <div class='party-info w-full md:w-1/2'>
                    <h3 class = ""italic"">Client:</h3>
                    <p><strong>Name:</strong> {{CLIENT_NAME}}</p>
                    <p><strong>Email:</strong> {{CLIENT_EMAIL}}</p>
                </div>

                <div class='party-info w-full md:w-1/2'>
                    <h3 class = ""italic"">Freelancer:</h3>
                    <p><strong>Name:</strong> {{FREELANCER_NAME}}</p>
                    <p><strong>Email:</strong> {{FREELANCER_EMAIL}}</p>
                </div>
            </div>
        </div>

                <div class='project-section mb-6'>
                    <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                        <h2 class=""text-center text-white font-bold"">PROJECT DETAILS</h2>
                    </div>
                    <p><strong>Project Name:</strong> {{PROJECT_NAME}}</p>
                    <p><strong>Description:</strong></p>
                    <div class='project-description'>{{PROJECT_DESCRIPTION}}</div>
                </div>

                <div class='terms-section mb-6'>
                    <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                        <h2 class=""text-center text-white font-bold"">TERMS AND CONDITIONS</h2>
                    </div>
                    
                    <h3 class = ""mb-1 italic"">1. Writing Fee</h3>
                    <p><strong>Total Project Fee:</strong> {{AGREED_AMOUNT}}</p>
                    {{PAYMENT_TERMS_SECTION}}
                    
                    <h3 class = ""mb-1 mt-4 italic"">2. Project Timeline</h3>
                    {{PROJECT_TIMELINE_SECTION}}
                    
                    <h3 class = ""mb-1 mt-4 italic"">3. Revision Policy</h3>
                    {{REVISION_POLICY_SECTION}}
                    
                    <h3 class = ""mb-1 mt-4 italic"">4. Content Deliverables</h3>
                    <p>All content will be delivered in the requested format (Word, Google Docs, HTML, etc.).</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">5. Research and Fact-Checking</h3>
                    <p>The Writer will conduct necessary research and ensure accuracy of factual content.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">6. Originality and Plagiarism</h3>
                    <p>All content will be original and plagiarism-free. The Writer guarantees no copyright infringement.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">7. SEO Optimization</h3>
                    <p>If applicable, content will be optimized for search engines according to provided guidelines.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">8. Usage Rights</h3>
                    <p>Upon payment, the Client receives full rights to use, modify, and publish the content.</p>
                    
                    <h3 class = ""mb-1 mt-4 italic"">9. Attribution</h3>
                    <p>Writer attribution requirements, if any, will be specified in the project details.</p>
                </div>

                <div class='agreement-section mb-6'>
                    <div class =""rounded-lg bg-blue-700 p-2 mb-2"">
                        <h2 class=""text-center text-white font-bold"">AGREEMENT</h2>
                    </div>
                    <p>By signing below, both parties agree to the terms and conditions set forth in this writing agreement.</p>
                </div>
            ";
        }
    }
}

