using Bentley.DgnPlatformNET;
using Bentley.DgnPlatformNET.Elements;

namespace GenDes.ItemTypes.Extensions
{
    public static class ElementExtensions
    {
        public static Element Clone(this Element elementToClone, DgnModel modelToCopyTo = null)
        {
            try
            {
                if (modelToCopyTo == null)
                    modelToCopyTo = elementToClone.DgnModel;

                using (ElementCopyContext cc = new ElementCopyContext(modelToCopyTo))
                {
                    cc.WriteElements = false;
                    cc.DisableAnnotationScaling = true;

                    Element clone = cc.DoCopy(elementToClone);
                    if (clone.AddToModel() == StatusInt.Success)
                        return clone;
                    else
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
