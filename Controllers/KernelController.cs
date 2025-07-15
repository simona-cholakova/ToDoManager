using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace TodoApi.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class PromptController : ControllerBase
    {
        private readonly Kernel _kernel;

        public PromptController(Kernel kernel)
        {
            _kernel = kernel;

            //register plugin correctly in v1.0.1
            if (!_kernel.Plugins.Contains("NativeFunctions"))
            {
                _kernel.Plugins.AddFromObject(new NativeFunctions(), "NativeFunctions");
            }
        }

        [HttpPost]
        public async Task<IActionResult> PromptText([FromBody] string inputText)
        {
            string skPrompt = @"Read this text and answer accordingly to what it wants.
                                Text to analyze: {{$userInput}}";

            var summarizeFunction = _kernel.CreateFunctionFromPrompt(
                skPrompt,
                functionName: "SummarizeText"
            );

            var result = await _kernel.InvokeAsync(summarizeFunction, new KernelArguments
            {
                ["userInput"] = inputText
            });

            return Ok(result.ToString());
        }

        [HttpGet("from-file")]
        public async Task<IActionResult> PromptFromFile([FromQuery] string fileName)
        {
            var fileFunc = _kernel.Plugins.GetFunction("NativeFunctions", "RetrieveLocalFileAsync");

            var result = await _kernel.InvokeAsync(fileFunc, new KernelArguments
            {
                ["fileName"] = fileName
            });

            return Ok(result.ToString());
        }
    }
}
