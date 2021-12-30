namespace Fabulous

open System.ComponentModel
open Fabulous
open Fabulous.StackAllocatedCollections
open Microsoft.FSharp.Core


type AttributesInFlight =
    (struct (StackArray3<ScalarAttribute> * WidgetAttribute [] option * WidgetCollectionAttribute [] option))

[<Struct>]
type WidgetBuilder<'msg, 'marker> =
    struct
        val Key: WidgetKey
        val Attributes: AttributesInFlight

        new(key: WidgetKey, attributes: AttributesInFlight) = { Key = key; Attributes = attributes }


        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member x.Compile() : Widget =
            let struct (scalarAttributes, widgetAttributes, widgetCollectionAttributes) = x.Attributes

            {
                Key = x.Key
#if DEBUG
                DebugName = $"{typeof<'marker>.Name}<{typeof<'msg>.Name}>"
#endif
                ScalarAttributes =
                    match StackArray3.length &scalarAttributes with
                    | 0 -> None
                    | _ -> Some(Array.sortInPlace(fun a -> a.Key) (StackArray3.toArray &scalarAttributes))

                WidgetAttributes =
                    widgetAttributes
                    |> Option.map(Array.sortInPlace(fun a -> a.Key))


                WidgetCollectionAttributes =
                    widgetCollectionAttributes
                    |> Option.map(Array.sortInPlace(fun a -> a.Key))
            }

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline x.AddScalar(attr: ScalarAttribute) =
            let struct (scalarAttributes, widgetAttributes, widgetCollectionAttributes) = x.Attributes
            //        let attribs = scalarAttributes
            //        let attribs2 = Array.zeroCreate(attribs.Length + 1)
            //        Array.blit attribs 0 attribs2 0 attribs.Length
            //        attribs2.[attribs.Length] <- attr

            WidgetBuilder<'msg, 'marker>(
                x.Key,
                struct (StackArray3.add(&scalarAttributes, attr), widgetAttributes, widgetCollectionAttributes)
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member x.AddWidget(attr: WidgetAttribute) =
            let struct (scalarAttributes, widgetAttributes, widgetCollectionAttributes) = x.Attributes
            let attribs = widgetAttributes

            let res =
                match attribs with
                | None -> [| attr |]
                | Some attribs ->
                    let attribs2 = Array.zeroCreate(attribs.Length + 1)
                    Array.blit attribs 0 attribs2 0 attribs.Length
                    attribs2.[attribs.Length] <- attr
                    attribs2

            WidgetBuilder<'msg, 'marker>(x.Key, struct (scalarAttributes, Some res, widgetCollectionAttributes))

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member x.AddWidgetCollection(attr: WidgetCollectionAttribute) =
            let struct (scalarAttributes, widgetAttributes, widgetCollectionAttributes) = x.Attributes
            let attribs = widgetCollectionAttributes

            let res =
                match attribs with
                | None -> [| attr |]
                | Some attribs ->
                    let attribs2 = Array.zeroCreate(attribs.Length + 1)
                    Array.blit attribs 0 attribs2 0 attribs.Length
                    attribs2.[attribs.Length] <- attr
                    attribs2

            WidgetBuilder<'msg, 'marker>(x.Key, struct (scalarAttributes, widgetAttributes, Some res))

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member x.AddScalars(attrs: ScalarAttribute []) =
            let struct (scalarAttributes, widgetAttributes, widgetCollectionAttributes) = x.Attributes
            //        if attrs.Length = 0 then
            //            x
            //        else
            //            let attribs = scalarAttributes
            //
            //            let attribs2 =
            //                Array.zeroCreate(attribs.Length + attrs.Length)
            //
            //            Array.blit attribs 0 attribs2 0 attribs.Length
            //            Array.blit attrs 0 attribs2 attribs.Length attrs.Length
            // TODO
            failwith "TODO"
    end

//        let t = (StackArray3.fromArray attrs)
//
//        let newScalars =
//            StackArray3.combine(&scalarAttributes, &t)
//
//        WidgetBuilder<'msg, 'marker>(key, struct (newScalars, widgetAttributes, widgetCollectionAttributes))

[<Struct>]
type Content<'msg> = { Widgets: StackArray3<Widget> }

[<Struct>]
type CollectionBuilder<'msg, 'marker, 'itemMarker> =
    struct
        val WidgetKey: WidgetKey
        val Scalars: StackArray3<ScalarAttribute>
        val Attr: WidgetCollectionAttributeDefinition

        new(widgetKey: WidgetKey, scalars: StackArray3<ScalarAttribute>, attr: WidgetCollectionAttributeDefinition) =
            {
                WidgetKey = widgetKey
                Scalars = scalars
                Attr = attr
            }

        member inline x.Run(c: Content<'msg>) =
            WidgetBuilder<'msg, 'marker>(
                x.WidgetKey,
                struct (x.Scalars, None, Some [| x.Attr.WithValue(StackArray3.toArray &c.Widgets) |])
            )

        member inline _.Combine(a: Content<'msg>, b: Content<'msg>) : Content<'msg> =
            {
                Widgets = StackArray3.combine a.Widgets b.Widgets
            }

        member inline _.Zero() : Content<'msg> = { Widgets = StackArray3.empty() }

        member inline _.Delay([<InlineIfLambda>] f) : Content<'msg> = f()

        member inline x.For<'t>(sequence: 't seq, [<InlineIfLambda>] f: 't -> Content<'msg>) : Content<'msg> =
            let mutable res: Content<'msg> = x.Zero()

            // this is essentially Fold, not sure what is more optimal
            // handwritten version of via Seq.Fold
            for t in sequence do
                res <- x.Combine(res, f t)

            res
    end

[<Struct>]
type AttributeCollectionBuilder<'msg, 'marker, 'itemMarker> =
    struct
        val Widget: WidgetBuilder<'msg, 'marker>
        val Attr: WidgetCollectionAttributeDefinition

        new(widget: WidgetBuilder<'msg, 'marker>, attr: WidgetCollectionAttributeDefinition) =
            { Widget = widget; Attr = attr }

        member inline x.Run(c: Content<'msg>) =
            x.Widget.AddWidgetCollection(x.Attr.WithValue(StackArray3.toArray &c.Widgets))

        member inline _.Combine(a: Content<'msg>, b: Content<'msg>) : Content<'msg> =
            {
                Widgets = StackArray3.combine a.Widgets b.Widgets
            }

        member inline _.Zero() : Content<'msg> = { Widgets = StackArray3.empty() }

        member inline _.Delay([<InlineIfLambda>] f) : Content<'msg> = f()

        member inline x.For<'t>(sequence: 't seq, [<InlineIfLambda>] f: 't -> Content<'msg>) : Content<'msg> =
            let mutable res: Content<'msg> = x.Zero()

            // this is essentially Fold, not sure what is more optimal
            // handwritten version of via Seq.Fold
            for t in sequence do
                res <- x.Combine(res, f t)

            res
    end
